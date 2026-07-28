using System.Collections.Concurrent;

namespace TrackNTrash.Tracking.Core.Stores;

/// <summary>
/// In-memory store implementations for local dev and unit tests. Thread-safe.
/// Production uses the SQL implementations (Api project) against the Module 1 schema.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, StoredScanEvent> _byIdemKey = new();
    private readonly ConcurrentQueue<StoredScanEvent> _all = new();
    private long _id;

    private static string Key(string deviceId, string clientEventId) => $"{deviceId}::{clientEventId}";

    public Task<(StoredScanEvent Event, bool Duplicate)> AppendOrGetAsync(ScanEventInput input, CancellationToken ct = default)
    {
        var key = Key(input.DeviceId, input.ClientEventId);
        if (_byIdemKey.TryGetValue(key, out var existing))
            return Task.FromResult((existing, true));

        var stored = new StoredScanEvent { ScanEventId = Interlocked.Increment(ref _id), Input = input };
        if (!_byIdemKey.TryAdd(key, stored))
            return Task.FromResult((_byIdemKey[key], true)); // race: someone inserted first

        _all.Enqueue(stored);
        return Task.FromResult((stored, false));
    }

    public Task<IReadOnlyList<StoredScanEvent>> GetByOrderLineAsync(long orderLineId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StoredScanEvent>>(
            _all.Where(e => e.Input.OrderLineId == orderLineId).OrderBy(e => e.ScanEventId).ToList());
}

public sealed class InMemoryShipmentStateStore : IShipmentStateStore
{
    private readonly ConcurrentDictionary<long, ShipmentLineStateRecord> _states = new();
    public readonly List<(long OrderLineId, ShipmentState? From, ShipmentState To, bool Legal)> History = new();

    public Task<ShipmentLineStateRecord> GetOrCreateAsync(long orderLineId, CancellationToken ct = default)
        => Task.FromResult(_states.GetOrAdd(orderLineId, id => new ShipmentLineStateRecord { OrderLineId = id }));

    public Task ApplyTransitionAsync(long orderLineId, TransitionResult result, long lastEventId, bool wasLegal, CancellationToken ct = default)
    {
        var rec = _states.GetOrAdd(orderLineId, id => new ShipmentLineStateRecord { OrderLineId = id });
        lock (rec)
        {
            var from = rec.CurrentState;
            if (wasLegal)
            {
                rec.PreviousState = rec.CurrentState;
                rec.CurrentState = result.ToState;
                rec.StateEnteredUtc = DateTimeOffset.UtcNow;
            }
            rec.LastEventId = lastEventId;
            History.Add((orderLineId, from, wasLegal ? result.ToState : from, wasLegal));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShipmentLineStateRecord>> GetByStateAsync(ShipmentState state, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ShipmentLineStateRecord>>(
            _states.Values.Where(s => s.CurrentState == state).ToList());
}

public sealed class InMemoryExceptionStore : IExceptionStore
{
    private readonly ConcurrentQueue<TrackException> _all = new();
    public Task AddAsync(TrackException ex, CancellationToken ct = default) { _all.Enqueue(ex); return Task.CompletedTask; }
    public Task<IReadOnlyList<TrackException>> GetOpenAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TrackException>>(_all.ToList());
}

public sealed class InMemoryManifestStore : IManifestStore
{
    private readonly ConcurrentDictionary<string, TrayManifest> _byTray = new();

    public Task UpsertAsync(TrayManifest manifest, CancellationToken ct = default)
    { _byTray[manifest.TrayQr] = manifest; return Task.CompletedTask; }

    public Task<TrayManifest?> GetAsync(string trayQr, CancellationToken ct = default)
        => Task.FromResult(_byTray.TryGetValue(trayQr, out var m) ? m : null);

    public Task<IReadOnlyList<TrayManifest>> GetChangedSinceAsync(DateTimeOffset since, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TrayManifest>>(
            _byTray.Values.Where(m => m.UpdatedUtc >= since).OrderBy(m => m.UpdatedUtc).ToList());
}
