namespace TrackNTrash.LabelApi.Services;

/// <summary>Thread-safe in-memory serial provider for local/dev/test (non-persistent).</summary>
public sealed class InMemorySerialNumberProvider : ISerialNumberProvider
{
    private long _carton;
    private long _sscc;
    private int _tray;

    public Task<long> NextCartonSerialAsync(CancellationToken ct = default)
        => Task.FromResult(Interlocked.Increment(ref _carton));

    public Task<long> NextSsccReferenceAsync(CancellationToken ct = default)
        => Task.FromResult(Interlocked.Increment(ref _sscc));

    public Task<int> NextTraySequenceAsync(CancellationToken ct = default)
        => Task.FromResult(Interlocked.Increment(ref _tray));
}
