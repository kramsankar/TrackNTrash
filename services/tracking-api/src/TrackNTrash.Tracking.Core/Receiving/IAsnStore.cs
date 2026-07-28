using System.Collections.Concurrent;

namespace TrackNTrash.Tracking.Core.Receiving;

public interface IAsnStore
{
    Task UpsertAsync(Asn asn, CancellationToken ct = default);
    Task<Asn?> GetAsync(string trayQr, string storeCode, CancellationToken ct = default);
    /// <summary>Which store a carton payload actually belongs to (for over-scan resolution).</summary>
    Task<string?> FindStoreForCartonAsync(string payload, CancellationToken ct = default);
}

public sealed class InMemoryAsnStore : IAsnStore
{
    private readonly ConcurrentDictionary<string, Asn> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private static string Key(string trayQr, string storeCode) => $"{trayQr}::{storeCode}";

    public Task UpsertAsync(Asn asn, CancellationToken ct = default)
    { _byKey[Key(asn.TrayQr, asn.StoreCode)] = asn; return Task.CompletedTask; }

    public Task<Asn?> GetAsync(string trayQr, string storeCode, CancellationToken ct = default)
        => Task.FromResult(_byKey.TryGetValue(Key(trayQr, storeCode), out var a) ? a : null);

    public Task<string?> FindStoreForCartonAsync(string payload, CancellationToken ct = default)
        => Task.FromResult(_byKey.Values
            .FirstOrDefault(a => a.ExpectedCartons.Any(c =>
                string.Equals(c.Payload, payload, StringComparison.OrdinalIgnoreCase)))?.StoreCode);
}
