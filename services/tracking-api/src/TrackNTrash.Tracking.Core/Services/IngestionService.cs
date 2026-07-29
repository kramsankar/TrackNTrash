using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Core.Services;

/// <summary>
/// Core ingestion pipeline. For each event:
///   1. Append append-only (idempotent on device+clientEventId).
///   2. Resolve the affected order line and map the event to a trigger.
///   3. Evaluate the state machine — legal edges advance the projection; illegal edges are
///      still recorded (event written) and raise an IllegalTransition exception. The event
///      write is NEVER blocked by a bad transition.
///   4. Run ingest-time exception rules.
///   5. Publish every raised exception to the notifier.
/// </summary>
public sealed class IngestionService
{
    private readonly IEventStore _events;
    private readonly IShipmentStateStore _states;
    private readonly IExceptionStore _exceptions;
    private readonly IManifestStore _manifests;
    private readonly INotificationPublisher _notifier;
    private readonly ShipmentStateMachine _machine;
    private readonly ExceptionSeverityMatrix _severity;
    private readonly IReadOnlyList<IIngestExceptionRule> _rules;
    private readonly ITrayProjection _trays;
    private readonly ILogger<IngestionService> _log;

    public IngestionService(
        IEventStore events,
        IShipmentStateStore states,
        IExceptionStore exceptions,
        IManifestStore manifests,
        INotificationPublisher notifier,
        ShipmentStateMachine machine,
        ExceptionSeverityMatrix severity,
        IEnumerable<IIngestExceptionRule> rules,
        ITrayProjection trays,
        ILogger<IngestionService> log)
    {
        _events = events;
        _states = states;
        _exceptions = exceptions;
        _manifests = manifests;
        _notifier = notifier;
        _machine = machine;
        _severity = severity;
        _rules = rules.ToList();
        _trays = trays;
        _log = log;
    }

    /// <summary>Maps an event onto the tray custody / contents projections.</summary>
    private async Task ProjectTrayAsync(ScanEventInput input, long scanEventId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.TrayQr)) return;
        try
        {
            switch (input.EventType)
            {
                case "TrayBuildComplete":
                    // The tray is built in the warehouse and holds the cartons just scanned.
                    await _trays.RecordCustodyAsync(input.TrayQr!, "Warehouse", null, null, scanEventId, ct);
                    var cartons = ParseCartonList(input.MetaJson);
                    if (cartons.Count > 0)
                        await _trays.BindCartonsAsync(input.TrayQr!, cartons, scanEventId, ct);
                    break;

                case "TripLoadScan":
                    await _trays.RecordCustodyAsync(input.TrayQr!, "Vehicle", input.TripId?.ToString(),
                        input.TripId, scanEventId, ct);
                    break;

                case "ReceivingComplete":
                case "TrayCustodyTransfer":
                    await _trays.RecordCustodyAsync(input.TrayQr!, "Store",
                        ExtractRef(input.MetaJson) ?? input.StoreId?.ToString(), input.TripId, scanEventId, ct);
                    break;

                case "EmptyTrayReturn":
                    await _trays.RecordCustodyAsync(input.TrayQr!, "Vehicle",
                        ExtractRef(input.MetaJson), input.TripId, scanEventId, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            // A projection failure must not lose the event — the log is the source of truth.
            _log.LogError(ex, "Tray projection failed for {Tray} on {Event}", input.TrayQr, input.EventType);
        }
    }

    /// <summary>Pulls a "ref" value out of an event's meta JSON without a JSON dependency.</summary>
    private static string? ExtractRef(string? metaJson) => ExtractString(metaJson, "\"ref\"");

    private static string? ExtractString(string? json, string marker)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var i = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var colon = json.IndexOf(':', i);
        if (colon < 0) return null;
        var start = json.IndexOf('"', colon + 1);
        if (start < 0) return null;
        var end = json.IndexOf('"', start + 1);
        return end < 0 ? null : json.Substring(start + 1, end - start - 1);
    }

    /// <summary>The pick app sends its carton list as a JSON array in meta.</summary>
    private static List<string> ParseCartonList(string? metaJson)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(metaJson)) return list;
        foreach (var part in metaJson.Split('"'))
            if (part.Length > 2 && !part.Contains(':') && !part.Contains('{') &&
                !part.Contains('[') && !part.Contains(',') &&
                part is not ("serial" or "orderLine" or "receiver" or "signature" or "ref" or "to" or "frameRef"))
                list.Add(part);
        return list;
    }

    public async Task<IngestResult> IngestAsync(ScanEventInput input, CancellationToken ct = default)
    {
        // 1. Append (idempotent).
        var (stored, duplicate) = await _events.AppendOrGetAsync(input, ct);
        if (duplicate)
        {
            _log.LogInformation("Duplicate event ignored: device={Device} clientEventId={Cid}",
                input.DeviceId, input.ClientEventId);
            return new IngestResult { Accepted = true, Duplicate = true, ScanEventId = stored.ScanEventId };
        }

        var raised = new List<TrackException>();
        ShipmentState? newState = null;
        bool transitionLegal = true;
        long? orderLineId = input.OrderLineId;

        // 2/3. State machine (only when the event maps to a trigger and targets a line).
        var trigger = EventTriggerMap.Resolve(input);
        ShipmentState? stateBefore = null;

        if (trigger is not null && orderLineId is not null)
        {
            var rec = await _states.GetOrCreateAsync(orderLineId.Value, ct);
            stateBefore = rec.CurrentState;

            var result = _machine.Evaluate(rec.CurrentState, trigger.Value);
            transitionLegal = result.IsLegal;

            await _states.ApplyTransitionAsync(orderLineId.Value, result, stored.ScanEventId, result.IsLegal, ct);

            if (result.IsLegal)
            {
                newState = result.ToState;
            }
            else
            {
                var ex = new TrackException
                {
                    Type = ExceptionType.IllegalTransition,
                    Severity = _severity.For(ExceptionType.IllegalTransition),
                    Checkpoint = input.Checkpoint,
                    OrderLineId = orderLineId,
                    TripId = input.TripId,
                    TrayId = input.TrayId,
                    TriggeringEventId = stored.ScanEventId,
                    Detail = $"Illegal transition: {result.FromState} --{result.Trigger}--> (expected {result.ToState}); " +
                             "event recorded, state unchanged."
                };
                raised.Add(ex);
            }
        }

        // 3b. Tray projections. Custody and contents are implied by events that are already
        // being recorded, so deriving them here keeps them consistent with the log.
        await ProjectTrayAsync(input, stored.ScanEventId, ct);

        // 4. Ingest-time rules.
        var ruleCtx = new IngestRuleContext(stored, orderLineId, stateBefore, _severity, _manifests);
        foreach (var rule in _rules)
        {
            try
            {
                raised.AddRange(await rule.EvaluateAsync(ruleCtx, ct));
            }
            catch (Exception rex)
            {
                _log.LogError(rex, "Ingest rule {Rule} failed", rule.Name);
            }
        }

        // 5. Persist + publish exceptions.
        foreach (var ex in raised)
        {
            await _exceptions.AddAsync(ex, ct);
            await _notifier.PublishAsync(ex, ct);
        }

        return new IngestResult
        {
            Accepted = true,
            Duplicate = false,
            ScanEventId = stored.ScanEventId,
            NewState = newState,
            TransitionLegal = transitionLegal,
            Exceptions = raised
        };
    }
}
