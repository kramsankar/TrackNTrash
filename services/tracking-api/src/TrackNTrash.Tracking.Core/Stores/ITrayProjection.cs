namespace TrackNTrash.Tracking.Core.Stores;

/// <summary>
/// Derives tray-level state from the event stream: who holds a tray, and which cartons
/// are in it. Both are projections of events that were already being recorded, so the
/// custody chain and tray contents stay consistent with the append-only log rather than
/// being written separately by each caller.
///
/// The no-op implementation lets local/dev runs and unit tests ignore it entirely.
/// </summary>
public interface ITrayProjection
{
    /// <summary>Records a change of custodian for a tray (Warehouse | Vehicle | Store).</summary>
    Task RecordCustodyAsync(string trayQr, string toCustodianType, string? toCustodianRef,
        long? tripId, long? scanEventId, CancellationToken ct = default);

    /// <summary>Binds cartons into a tray at tray-build time.</summary>
    Task BindCartonsAsync(string trayQr, IReadOnlyList<string> cartonPayloads,
        long? scanEventId, CancellationToken ct = default);
}

/// <summary>Used when there is no database to project into.</summary>
public sealed class NoOpTrayProjection : ITrayProjection
{
    public Task RecordCustodyAsync(string trayQr, string toCustodianType, string? toCustodianRef,
        long? tripId, long? scanEventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task BindCartonsAsync(string trayQr, IReadOnlyList<string> cartonPayloads,
        long? scanEventId, CancellationToken ct = default) => Task.CompletedTask;
}
