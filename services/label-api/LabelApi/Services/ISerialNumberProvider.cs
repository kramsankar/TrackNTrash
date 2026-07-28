namespace TrackNTrash.LabelApi.Services;

/// <summary>
/// Supplies monotonic serial references. Backed by DB sequences
/// (ref.SsccSerialReference, ref.CartonSerialReference, ref.TraySequence) in production,
/// or an in-memory counter for local/dev/test.
/// </summary>
public interface ISerialNumberProvider
{
    Task<long> NextCartonSerialAsync(CancellationToken ct = default);
    Task<long> NextSsccReferenceAsync(CancellationToken ct = default);
    Task<int> NextTraySequenceAsync(CancellationToken ct = default);
}
