using System.Collections.Concurrent;
using TrackNTrash.Tracking.Core.Receiving;

namespace TrackNTrash.Tracking.Api;

/// <summary>
/// Holds active receiving sessions server-side for the thin-client API path. In the MAUI app
/// the session lives on-device (offline); on completion it posts the summary + scan events.
///
/// SQL-backed when a connection string is configured, so a recycle mid-round does not make
/// the colleague at the door restart the tray from the first carton.
/// </summary>
public interface IReceivingSessionStore
{
    Task<string> AddAsync(ReceivingSession session, CancellationToken ct = default);
    Task<ReceivingSession?> GetAsync(string id, CancellationToken ct = default);
    /// <summary>Persists what has been scanned so far; a no-op for the in-memory store.</summary>
    Task SaveAsync(string id, ReceivingSession session, CancellationToken ct = default);
    Task RemoveAsync(string id, CancellationToken ct = default);
}

/// <summary>In-memory sessions, used for local runs and tests with no database.</summary>
public sealed class InMemoryReceivingSessionStore : IReceivingSessionStore
{
    private readonly ConcurrentDictionary<string, ReceivingSession> _sessions = new();
    private long _seq;

    public Task<string> AddAsync(ReceivingSession session, CancellationToken ct = default)
    {
        var id = $"recv-{Interlocked.Increment(ref _seq):D6}";
        _sessions[id] = session;
        return Task.FromResult(id);
    }

    public Task<ReceivingSession?> GetAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_sessions.TryGetValue(id, out var s) ? s : null);

    // The stored object is the same instance the endpoint mutated, so there is nothing to write.
    public Task SaveAsync(string id, ReceivingSession session, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        _sessions.TryRemove(id, out _);
        return Task.CompletedTask;
    }
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
