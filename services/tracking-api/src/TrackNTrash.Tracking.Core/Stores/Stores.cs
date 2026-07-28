namespace TrackNTrash.Tracking.Core.Stores;

/// <summary>Append-only event log. Idempotent on (DeviceId, ClientEventId).</summary>
public interface IEventStore
{
    /// <summary>Appends the event, or returns the existing one if the idempotency key was seen.</summary>
    Task<(StoredScanEvent Event, bool Duplicate)> AppendOrGetAsync(ScanEventInput input, CancellationToken ct = default);

    Task<IReadOnlyList<StoredScanEvent>> GetByOrderLineAsync(long orderLineId, CancellationToken ct = default);
}

/// <summary>Derived shipment-line state projection + transition history.</summary>
public interface IShipmentStateStore
{
    Task<ShipmentLineStateRecord> GetOrCreateAsync(long orderLineId, CancellationToken ct = default);
    Task ApplyTransitionAsync(long orderLineId, TransitionResult result, long lastEventId, bool wasLegal, CancellationToken ct = default);
    /// <summary>Order lines currently in the given state (for time-based sweeps).</summary>
    Task<IReadOnlyList<ShipmentLineStateRecord>> GetByStateAsync(ShipmentState state, CancellationToken ct = default);
}

public interface IExceptionStore
{
    Task AddAsync(TrackException ex, CancellationToken ct = default);
    Task<IReadOnlyList<TrackException>> GetOpenAsync(CancellationToken ct = default);
}

public interface IManifestStore
{
    Task UpsertAsync(TrayManifest manifest, CancellationToken ct = default);
    Task<TrayManifest?> GetAsync(string trayQr, CancellationToken ct = default);
    /// <summary>Delta sync: manifests updated at/after the given timestamp.</summary>
    Task<IReadOnlyList<TrayManifest>> GetChangedSinceAsync(DateTimeOffset since, CancellationToken ct = default);
}
