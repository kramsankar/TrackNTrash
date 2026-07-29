using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Reusable-asset (tray) master over ops.Tray. Registers trays with generated GS1-style
/// internal QR values (TRAY-{site}-{seq}) and lists them with status + current custodian.
/// </summary>
public sealed class SqlAssetStore
{
    private readonly string _cs;
    public SqlAssetStore(string cs) => _cs = cs;

    public sealed record AssetRow(int TrayId, string TrayQr, string SiteCode, string TrayStatus,
        string CurrentCustodianType, string? CurrentCustodianRef, DateTimeOffset? LastSeenUtc, DateTimeOffset CreatedUtc);

    /// <summary>Registers <paramref name="count"/> new trays for a site; returns their QR values.</summary>
    public async Task<IReadOnlyList<string>> RegisterTraysAsync(string siteCode, int count, CancellationToken ct = default)
    {
        var site = new string(siteCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (site.Length == 0) throw new ArgumentException("siteCode required");
        if (count is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(count), "1..500");

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        var created = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            const string sql = @"
DECLARE @seq INT = NEXT VALUE FOR ref.TraySequence;
DECLARE @qr NVARCHAR(30) = 'TRAY-' + @site + '-' + RIGHT('000000' + CAST(@seq AS varchar(6)), 6);
INSERT INTO ops.Tray (TrayQr, SiteCode, TrayStatus, CurrentCustodianType, CurrentCustodianRef)
VALUES (@qr, @site, 'Available', 'Warehouse', @site);
SELECT @qr;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@site", site);
            created.Add((string)(await cmd.ExecuteScalarAsync(ct))!);
        }
        return created;
    }

    public async Task<IReadOnlyList<AssetRow>> ListAsync(int top = 1000, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT TOP (@top) TrayId, TrayQr, SiteCode, TrayStatus, CurrentCustodianType, CurrentCustodianRef, LastSeenUtc, CreatedUtc
FROM ops.Tray ORDER BY TrayId DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@top", top);
        var list = new List<AssetRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new AssetRow(
                r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : new DateTimeOffset(r.GetDateTime(6), TimeSpan.Zero),
                new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero)));
        return list;
    }

    public sealed record AssetSummary(int Total, int Available, int InUse, int InTransit, int AtStore, int Lost);

    public async Task<AssetSummary> SummaryAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT COUNT(*) AS Total,
  SUM(CASE WHEN TrayStatus='Available' THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrayStatus='InUse' THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrayStatus='InTransit' THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrayStatus='AtStore' THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrayStatus IN ('Lost','WrittenOff') THEN 1 ELSE 0 END)
FROM ops.Tray;";
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        int G(int i) => r.IsDBNull(i) ? 0 : r.GetInt32(i);
        return new AssetSummary(G(0), G(1), G(2), G(3), G(4), G(5));
    }
}
