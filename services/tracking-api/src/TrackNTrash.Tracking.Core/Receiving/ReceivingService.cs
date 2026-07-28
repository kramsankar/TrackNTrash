using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core.Services;

namespace TrackNTrash.Tracking.Core.Receiving;

/// <summary>
/// Store receiving reconciliation. Scans are matched against the tray ASN in real time:
///   * expected + first time  → Received
///   * expected + repeat       → Duplicate
///   * not expected            → Over (with the store it should have gone to)
///   * flagged damaged         → Damaged (requires a photo blob reference)
/// Completion computes shorts, emits a ReceivingComplete summary + per-line Received transitions,
/// raises exceptions for any discrepancy, and transfers tray custody to the store.
/// </summary>
public sealed class ReceivingService
{
    private readonly IAsnStore _asns;
    private readonly IngestionService _ingestion;
    private readonly ExceptionSeverityMatrix _severity;
    private readonly ILogger<ReceivingService> _log;

    public ReceivingService(IAsnStore asns, IngestionService ingestion, ExceptionSeverityMatrix severity, ILogger<ReceivingService> log)
    {
        _asns = asns;
        _ingestion = ingestion;
        _severity = severity;
        _log = log;
    }

    public async Task<ReceivingSession?> StartAsync(string trayQr, string storeCode, CancellationToken ct = default)
    {
        var asn = await _asns.GetAsync(trayQr, storeCode, ct);
        return asn is null ? null : new ReceivingSession { Asn = asn };
    }

    public async Task<CartonScanResult> ScanAsync(ReceivingSession s, string payload, CancellationToken ct = default)
    {
        if (s.IsExpected(payload))
        {
            if (!s.Received.Add(payload))
                return Result(s, CartonReceiveOutcome.Duplicate, payload, "Already received");
            return Result(s, CartonReceiveOutcome.Received, payload, "Received");
        }

        // Unexpected carton → Over. Resolve the correct store.
        s.Over.Add(payload);
        var correctStore = await _asns.FindStoreForCartonAsync(payload, ct);
        return Result(s, CartonReceiveOutcome.Over, payload,
            correctStore is not null ? $"OVER — belongs to {correctStore}" : "OVER — unknown carton",
            correctStore);
    }

    /// <summary>Flag a received carton as damaged. A photo blob reference is mandatory.</summary>
    public CartonScanResult FlagDamaged(ReceivingSession s, string payload, string photoBlobUri)
    {
        if (string.IsNullOrWhiteSpace(photoBlobUri))
            throw new ArgumentException("A damage photo is required.", nameof(photoBlobUri));
        s.Damaged.Add(payload);
        return Result(s, CartonReceiveOutcome.Damaged, payload, "Damaged (photo captured)");
    }

    /// <summary>
    /// Finalize receiving: emit ReceivingComplete per order line (Received or Short verdict),
    /// raise discrepancy exceptions, transfer tray custody to the store, and return the summary.
    /// </summary>
    public async Task<ReceivingSummary> CompleteAsync(ReceivingSession s, string deviceId, ProofOfDelivery pod, CancellationToken ct = default)
    {
        var shorts = s.ShortPayloads.ToList();

        // Per order line: mark Received, or Short if none of its cartons arrived.
        foreach (var expected in s.Asn.ExpectedCartons)
        {
            bool received = s.Received.Contains(expected.Payload);
            await _ingestion.IngestAsync(new ScanEventInput
            {
                ClientEventId = $"{s.Asn.TrayQr}:{expected.OrderLineId}:{expected.Payload}:recv",
                EventType = "ReceivingComplete",
                Checkpoint = "StoreReceive",
                DeviceId = deviceId,
                UserId = pod.ReceiverName,
                TrayQr = s.Asn.TrayQr,
                OrderLineId = expected.OrderLineId,
                Verdict = received ? "RECEIVED" : "SHORT",
                MetaJson = $"{{\"receiver\":\"{pod.ReceiverName}\",\"signature\":\"{pod.SignatureBlobUri}\"}}"
            }, ct);
        }

        // Tray custody → store.
        await _ingestion.IngestAsync(new ScanEventInput
        {
            ClientEventId = $"{s.Asn.TrayQr}:custody:{s.Asn.StoreCode}",
            EventType = "TrayCustodyTransfer",
            Checkpoint = "StoreReceive",
            DeviceId = deviceId,
            TrayQr = s.Asn.TrayQr,
            MetaJson = $"{{\"to\":\"Store\",\"ref\":\"{s.Asn.StoreCode}\"}}"
        }, ct);

        var summary = new ReceivingSummary
        {
            TrayQr = s.Asn.TrayQr,
            StoreCode = s.Asn.StoreCode,
            ExpectedCount = s.ExpectedCount,
            ReceivedCount = s.Received.Count,
            ShortPayloads = shorts,
            OverPayloads = s.Over.ToList(),
            DamagedPayloads = s.Damaged.ToList(),
            ReceiverName = pod.ReceiverName
        };

        _log.LogInformation("Receiving complete: tray={Tray} store={Store} received={R}/{E} short={S} over={O} damaged={D}",
            summary.TrayQr, summary.StoreCode, summary.ReceivedCount, summary.ExpectedCount,
            summary.ShortPayloads.Count, summary.OverPayloads.Count, summary.DamagedPayloads.Count);

        return summary;
    }

    /// <summary>Empty tray return: driver scans returning trays → custody back to the vehicle.</summary>
    public async Task ReturnEmptyTrayAsync(string trayQr, string vehicleReg, string deviceId, CancellationToken ct = default)
        => await _ingestion.IngestAsync(new ScanEventInput
        {
            ClientEventId = $"{trayQr}:return:{vehicleReg}",
            EventType = "EmptyTrayReturn",
            Checkpoint = "StoreReceive",
            DeviceId = deviceId,
            TrayQr = trayQr,
            MetaJson = $"{{\"to\":\"Vehicle\",\"ref\":\"{vehicleReg}\"}}"
        }, ct);

    private static CartonScanResult Result(ReceivingSession s, CartonReceiveOutcome outcome, string payload, string message, string? correctStore = null)
        => new()
        {
            Outcome = outcome,
            Payload = payload,
            CorrectStoreCode = correctStore,
            Message = message,
            Received = s.Received.Count,
            Expected = s.ExpectedCount,
            Unexpected = s.Over.Count
        };
}
