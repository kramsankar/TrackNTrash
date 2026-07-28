using System.Collections.Concurrent;
using TrackNTrash.Tracking.Core.Receiving;

namespace TrackNTrash.Tracking.Api;

/// <summary>
/// Holds active receiving sessions server-side for the thin-client API path. In the MAUI app
/// the session lives on-device (offline); on completion it posts the summary + scan events.
/// </summary>
public sealed class ReceivingSessionCache
{
    private readonly ConcurrentDictionary<string, ReceivingSession> _sessions = new();
    private long _seq;

    public string Add(ReceivingSession session)
    {
        var id = $"recv-{Interlocked.Increment(ref _seq):D6}";
        _sessions[id] = session;
        return id;
    }

    public ReceivingSession? Get(string id) => _sessions.TryGetValue(id, out var s) ? s : null;
    public void Remove(string id) => _sessions.TryRemove(id, out _);
}

public sealed record AsnDto
{
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public List<ExpectedCartonDto> ExpectedCartons { get; init; } = new();

    public Asn ToAsn() => new()
    {
        TrayQr = TrayQr,
        StoreCode = StoreCode,
        ExpectedCartons = ExpectedCartons.Select(c => new ExpectedCarton
        { Payload = c.Payload, OrderLineId = c.OrderLineId, Gtin = c.Gtin }).ToList()
    };
}

public sealed record ExpectedCartonDto
{
    public string Payload { get; init; } = "";
    public long OrderLineId { get; init; }
    public string? Gtin { get; init; }
}

public sealed record StartReceivingDto
{
    public string TrayQr { get; init; } = "";
    public string StoreCode { get; init; } = "";
}

public sealed record ScanCartonDto { public string Payload { get; init; } = ""; }

public sealed record DamagedDto
{
    public string Payload { get; init; } = "";
    public string PhotoBlobUri { get; init; } = "";
}

public sealed record CompleteReceivingDto
{
    public string DeviceId { get; init; } = "";
    public string ReceiverName { get; init; } = "";
    public string? SignatureBlobUri { get; init; }
    public string? DeliveryPhotoBlobUri { get; init; }
}

public sealed record ReturnTrayDto
{
    public string TrayQr { get; init; } = "";
    public string VehicleReg { get; init; } = "";
    public string DeviceId { get; init; } = "";
}
