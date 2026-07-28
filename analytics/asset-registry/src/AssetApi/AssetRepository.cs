using Microsoft.Data.SqlClient;

namespace TrackNTrash.AssetApi;

public sealed record CustodyRecord(string From, string To, DateTimeOffset Utc, long? TripId, string? Note);
public sealed record AssetSummary(int CirculatingTrays, decimal AvgCycleDays, decimal DailyDemand,
    decimal TargetUtilization, int RecommendedFleet, decimal WorstRouteLossPct, string? WorstRoute);
public sealed record AssetException(long ExceptionId, string Type, string Severity, int? TrayId, string Detail, DateTimeOffset CreatedUtc);

public interface IAssetRepository
{
    Task<IReadOnlyList<CustodyRecord>> GetHistoryAsync(string trayQr, CancellationToken ct = default);
    Task<AssetSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssetException>> GetExceptionsAsync(CancellationToken ct = default);
    Task RecomputeAsync(CancellationToken ct = default);
}

/// <summary>SQL-backed repository over the Module 1 + Module 10 (asset schema) tables.</summary>
public sealed class SqlAssetRepository : IAssetRepository
{
    private readonly string _cs;
    public SqlAssetRepository(string cs) => _cs = cs;

    public async Task<IReadOnlyList<CustodyRecord>> GetHistoryAsync(string trayQr, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tc.FromCustodianType, tc.FromCustodianRef, tc.ToCustodianType, tc.ToCustodianRef,
       tc.CustodyUtc, tc.TripId, tc.Note
FROM ops.TrayCustody tc JOIN ops.Tray t ON t.TrayId = tc.TrayId
WHERE t.TrayQr = @qr ORDER BY tc.CustodyUtc;";
        var list = new List<CustodyRecord>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qr", trayQr);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CustodyRecord(
                $"{Safe(r,0)}:{Safe(r,1)}", $"{Safe(r,2)}:{Safe(r,3)}",
                new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero),
                r.IsDBNull(5) ? null : r.GetInt64(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return list;
    }

    public async Task<AssetSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT TOP 1 CirculatingTrays, AvgCycleDays, DailyDemandTrays, TargetUtilization, RecommendedFleet
FROM asset.FleetRecommendation ORDER BY SnapshotDate DESC;
SELECT TOP 1 DimensionKey, LossRatePct FROM asset.LossRate
WHERE Dimension = 'Route' ORDER BY SnapshotDate DESC, LossRatePct DESC;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        int circ = 0; decimal cyc = 0, demand = 0, target = 0; int fleet = 0;
        if (await r.ReadAsync(ct))
        { circ = r.GetInt32(0); cyc = r.GetDecimal(1); demand = r.GetDecimal(2); target = r.GetDecimal(3); fleet = r.GetInt32(4); }
        string? worstRoute = null; decimal worstLoss = 0;
        if (await r.NextResultAsync(ct) && await r.ReadAsync(ct))
        { worstRoute = r.GetString(0); worstLoss = r.GetDecimal(1); }
        return new AssetSummary(circ, cyc, demand, target, fleet, worstLoss, worstRoute);
    }

    public async Task<IReadOnlyList<AssetException>> GetExceptionsAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT ExceptionId, ExceptionType, Severity, TrayId, Detail, CreatedUtc
FROM ops.Exception
WHERE ExceptionType IN ('SuspectedLost','TrayDwellExceeded') AND Status IN ('Open','Acknowledged','Escalated')
ORDER BY CreatedUtc DESC;";
        var list = new List<AssetException>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new AssetException(r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt32(3), r.GetString(4),
                new DateTimeOffset(r.GetDateTime(5), TimeSpan.Zero)));
        return list;
    }

    public async Task RecomputeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("asset.usp_ComputeNightlyMetrics", conn)
        { CommandType = System.Data.CommandType.StoredProcedure, CommandTimeout = 300 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Safe(SqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
}

/// <summary>Demo repository so the API runs without a database (sample data).</summary>
public sealed class DemoAssetRepository : IAssetRepository
{
    public Task<IReadOnlyList<CustodyRecord>> GetHistoryAsync(string trayQr, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CustodyRecord>>(new[]
        {
            new CustodyRecord(":", "Warehouse:LDN1", DateTimeOffset.UtcNow.AddDays(-6), null, "created"),
            new CustodyRecord("Warehouse:LDN1", "Vehicle:AB12CDE", DateTimeOffset.UtcNow.AddDays(-2), 1, "loaded"),
            new CustodyRecord("Vehicle:AB12CDE", "Store:S-101", DateTimeOffset.UtcNow.AddDays(-1), 1, "delivered"),
        });

    public Task<AssetSummary> GetSummaryAsync(CancellationToken ct = default)
        => Task.FromResult(new AssetSummary(1200, 5.2m, 180m, 0.80m, 1170, 4.5m, "R-NORTH"));

    public Task<IReadOnlyList<AssetException>> GetExceptionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AssetException>>(new[]
        {
            new AssetException(1, "SuspectedLost", "Medium", 42, "Tray TRAY-LDN1-000042 not seen since 2026-07-05", DateTimeOffset.UtcNow.AddDays(-1))
        });

    public Task RecomputeAsync(CancellationToken ct = default) => Task.CompletedTask;
}
