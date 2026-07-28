using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Core.Rules;

/// <summary>Context for an ingest-time rule: the event just written + resolved state.</summary>
public sealed record IngestRuleContext(
    StoredScanEvent Event,
    long? OrderLineId,
    ShipmentState? StateBefore,
    ExceptionSeverityMatrix Severity,
    IManifestStore Manifests);

/// <summary>Rule evaluated synchronously as each event is ingested.</summary>
public interface IIngestExceptionRule
{
    string Name { get; }
    Task<IEnumerable<TrackException>> EvaluateAsync(IngestRuleContext ctx, CancellationToken ct = default);
}

/// <summary>Context for a time-based sweep rule.</summary>
public sealed record SweepRuleContext(
    DateTimeOffset Now,
    IShipmentStateStore States,
    ExceptionSeverityMatrix Severity,
    TimeSpan ReceiveSla,
    TimeSpan TrayDwellLimit);

/// <summary>Rule evaluated periodically by the timer sweep.</summary>
public interface ISweepExceptionRule
{
    string Name { get; }
    Task<IEnumerable<TrackException>> EvaluateAsync(SweepRuleContext ctx, CancellationToken ct = default);
}

// ------------------------------------------------------------------------------------
// Example rule #1 (ingest-driven): dock count mismatch.
// Fires when a DockVerification event arrives with a non-PASS verdict. Compares the
// decoded/detected counts in the event payload against the tray's expected manifest.
// ------------------------------------------------------------------------------------
public sealed class CountMismatchAtDockRule : IIngestExceptionRule
{
    public string Name => "CountMismatchAtDock";

    public async Task<IEnumerable<TrackException>> EvaluateAsync(IngestRuleContext ctx, CancellationToken ct = default)
    {
        var e = ctx.Event.Input;
        if (!string.Equals(e.EventType, "DockVerification", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<TrackException>();

        var verdict = e.Verdict ?? "UNKNOWN";
        if (string.Equals(verdict, "PASS", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<TrackException>();

        // Map the dock verdict to an exception type.
        var type = verdict.ToUpperInvariant() switch
        {
            "COUNT_MISMATCH" => ExceptionType.CountMismatch,
            "UNKNOWN_CARTON" => ExceptionType.UnknownCarton,
            "MISSING_CARTON" => ExceptionType.MissingCarton,
            _                => ExceptionType.CountMismatch
        };

        int? expected = null;
        if (e.TrayQr is not null)
        {
            var manifest = await ctx.Manifests.GetAsync(e.TrayQr, ct);
            expected = manifest?.ExpectedCartonCount;
        }

        var detail = $"Dock verdict {verdict} for tray {e.TrayQr ?? "?"}"
                     + (expected is not null ? $" (expected {expected} cartons)" : "");

        return new[]
        {
            new TrackException
            {
                Type = type,
                Severity = ctx.Severity.For(type),
                Checkpoint = "DispatchDock",
                OrderLineId = ctx.OrderLineId,
                TripId = e.TripId,
                TriggeringEventId = ctx.Event.ScanEventId,
                Detail = detail,
                FrameBlobUri = TryGetFrameRef(e.MetaJson)
            }
        };
    }

    private static string? TryGetFrameRef(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson)) return null;
        // Lightweight extraction without a JSON dependency in Core.
        const string marker = "\"frameRef\"";
        var idx = metaJson.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var colon = metaJson.IndexOf(':', idx);
        if (colon < 0) return null;
        var start = metaJson.IndexOf('"', colon + 1);
        if (start < 0) return null;
        var end = metaJson.IndexOf('"', start + 1);
        return end < 0 ? null : metaJson.Substring(start + 1, end - start - 1);
    }
}

// ------------------------------------------------------------------------------------
// Example rule #2 (time-swept): no Received event within SLA of being Loaded.
// Any line still in Loaded/InTransit past the SLA is flagged NoReceiveSla; well past it,
// escalated to SuspectedLost. Evaluated by the periodic timer, not on ingest.
// ------------------------------------------------------------------------------------
public sealed class NoReceiveWithinSlaRule : ISweepExceptionRule
{
    public string Name => "NoReceiveWithinSla";

    public async Task<IEnumerable<TrackException>> EvaluateAsync(SweepRuleContext ctx, CancellationToken ct = default)
    {
        var result = new List<TrackException>();

        foreach (var state in new[] { ShipmentState.Loaded, ShipmentState.InTransit })
        {
            var lines = await ctx.States.GetByStateAsync(state, ct);
            foreach (var line in lines)
            {
                var age = ctx.Now - line.StateEnteredUtc;
                if (age <= ctx.ReceiveSla) continue;

                var lost = age > ctx.ReceiveSla + ctx.ReceiveSla; // 2x SLA -> suspected lost
                var type = lost ? ExceptionType.SuspectedLost : ExceptionType.NoReceiveSla;

                result.Add(new TrackException
                {
                    Type = type,
                    Severity = ctx.Severity.For(type),
                    Checkpoint = "StoreReceive",
                    OrderLineId = line.OrderLineId,
                    TriggeringEventId = line.LastEventId,
                    Detail = $"Line {line.OrderLineId} in {state} for {age.TotalHours:F1}h "
                             + $"(SLA {ctx.ReceiveSla.TotalHours:F0}h) — {(lost ? "suspected lost" : "receive SLA breach")}"
                });
            }
        }
        return result;
    }
}
