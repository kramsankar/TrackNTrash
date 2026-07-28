using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Services;

namespace TrackNTrash.Tracking.Functions;

/// <summary>
/// Consumes DockVerification events from IoT Hub (routed to an Event Hub-compatible endpoint)
/// and feeds them through the shared ingestion pipeline. Non-PASS verdicts raise exceptions
/// via CountMismatchAtDockRule; the event is always written append-only.
/// </summary>
public sealed class DockVerificationFunction
{
    private readonly IngestionService _ingestion;
    private readonly ILogger<DockVerificationFunction> _log;

    public DockVerificationFunction(IngestionService ingestion, ILogger<DockVerificationFunction> log)
    {
        _ingestion = ingestion;
        _log = log;
    }

    [Function("DockVerification")]
    public async Task Run(
        [EventHubTrigger("%DockEventHubName%", Connection = "IoTHubEventHub", IsBatched = true)]
        string[] messages,
        FunctionContext context)
    {
        foreach (var raw in messages)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<DockVerificationMessage>(raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg is null) continue;

                var input = new ScanEventInput
                {
                    ClientEventId = msg.ClientEventId ?? $"{msg.TrayQr}:{msg.EventUtc:O}",
                    EventType = "DockVerification",
                    Checkpoint = "DispatchDock",
                    DeviceId = msg.DeviceId ?? "edge-dock",
                    TrayQr = msg.TrayQr,
                    TripId = msg.TripId,
                    OrderLineId = msg.OrderLineId,
                    Verdict = msg.Verdict,
                    MetaJson = raw,
                    EventUtc = msg.EventUtc == default ? DateTimeOffset.UtcNow : msg.EventUtc
                };

                var result = await _ingestion.IngestAsync(input, context.CancellationToken);
                _log.LogInformation("Dock event ingested: tray={Tray} verdict={Verdict} exceptions={Count}",
                    msg.TrayQr, msg.Verdict, result.Exceptions.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to process dock verification message");
                // Do not rethrow single-message failures in a batch; poison handling via host retry/DLQ.
            }
        }
    }

    private sealed record DockVerificationMessage
    {
        public string? ClientEventId { get; init; }
        public string? DeviceId { get; init; }
        public string? TrayQr { get; init; }
        public long? TripId { get; init; }
        public long? OrderLineId { get; init; }
        public string? Verdict { get; init; }
        public int DetectedCount { get; init; }
        public int ExpectedCount { get; init; }
        public DateTimeOffset EventUtc { get; init; }
    }
}
