using System.Collections.Concurrent;

namespace TrackNTrash.Tracking.Core.Trips;

public interface ITripStore
{
    Task<Trip> AddAsync(Trip trip, CancellationToken ct = default);
    /// <summary>Next identity for a trip number. Must be unique across process restarts.</summary>
    Task<long> NextSequenceAsync(CancellationToken ct = default);
    Task<Trip?> GetByNumberAsync(string tripNumber, CancellationToken ct = default);
    Task<Trip?> GetByManifestQrAsync(string manifestQr, CancellationToken ct = default);
    /// <summary>Find the trip a tray is planned on (for wrong-trip detection).</summary>
    Task<Trip?> FindTripForTrayAsync(string trayQr, CancellationToken ct = default);
    Task UpdateAsync(Trip trip, CancellationToken ct = default);
}

public sealed class InMemoryTripStore : ITripStore
{
    private readonly ConcurrentDictionary<string, Trip> _byNumber = new(StringComparer.OrdinalIgnoreCase);
    private long _id;

    public Task<Trip> AddAsync(Trip trip, CancellationToken ct = default)
    {
        _byNumber[trip.TripNumber] = trip;
        return Task.FromResult(trip);
    }

    public Task<long> NextSequenceAsync(CancellationToken ct = default)
        => Task.FromResult(Interlocked.Increment(ref _id));

    public Task<Trip?> GetByNumberAsync(string tripNumber, CancellationToken ct = default)
        => Task.FromResult(_byNumber.TryGetValue(tripNumber, out var t) ? t : null);

    public Task<Trip?> GetByManifestQrAsync(string manifestQr, CancellationToken ct = default)
        => Task.FromResult(_byNumber.Values.FirstOrDefault(t =>
            string.Equals(t.ManifestQr, manifestQr, StringComparison.OrdinalIgnoreCase)));

    public Task<Trip?> FindTripForTrayAsync(string trayQr, CancellationToken ct = default)
        => Task.FromResult(_byNumber.Values.FirstOrDefault(t => t.FindTray(trayQr) is not null));

    public Task UpdateAsync(Trip trip, CancellationToken ct = default)
    {
        _byNumber[trip.TripNumber] = trip;
        return Task.CompletedTask;
    }
}
