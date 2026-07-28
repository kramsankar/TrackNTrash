using System.Text.Json;
using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>SQL-backed shipment-line state projection over ops.ShipmentLineState + history.</summary>
public sealed class SqlShipmentStateStore : IShipmentStateStore
{
    private readonly string _cs;
    public SqlShipmentStateStore(string cs) => _cs = cs;

    public async Task<ShipmentLineStateRecord> GetOrCreateAsync(long orderLineId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        // Create a default (Ordered) projection row if absent. Requires the order line to exist
        // (FK) — order lines are created via the /orders intake or D365.
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM ops.ShipmentLineState WHERE OrderLineId = @ol)
    INSERT INTO ops.ShipmentLineState (OrderLineId, CurrentState) VALUES (@ol, 'Ordered');
SELECT OrderLineId, CurrentState, PreviousState, PickedCartons, ReceivedCartons, LastEventId, StateEnteredUtc
FROM ops.ShipmentLineState WHERE OrderLineId = @ol;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ol", orderLineId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return Map(r);
    }

    public async Task ApplyTransitionAsync(long orderLineId, TransitionResult result, long lastEventId, bool wasLegal, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
DECLARE @from VARCHAR(20) = (SELECT CurrentState FROM ops.ShipmentLineState WHERE OrderLineId = @ol);
IF @legal = 1
    UPDATE ops.ShipmentLineState
       SET PreviousState = CurrentState, CurrentState = @to, LastEventId = @evt, StateEnteredUtc = SYSUTCDATETIME()
     WHERE OrderLineId = @ol;
ELSE
    UPDATE ops.ShipmentLineState SET LastEventId = @evt WHERE OrderLineId = @ol;
INSERT INTO ops.ShipmentLineStateHistory (OrderLineId, FromState, ToState, ScanEventId, WasLegal)
VALUES (@ol, @from, CASE WHEN @legal = 1 THEN @to ELSE @from END, @evt, @legal);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ol", orderLineId);
        cmd.Parameters.AddWithValue("@to", result.ToState.ToString());
        cmd.Parameters.AddWithValue("@evt", lastEventId);
        cmd.Parameters.AddWithValue("@legal", wasLegal ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ShipmentLineStateRecord>> GetByStateAsync(ShipmentState state, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"SELECT OrderLineId, CurrentState, PreviousState, PickedCartons, ReceivedCartons, LastEventId, StateEnteredUtc
                             FROM ops.ShipmentLineState WHERE CurrentState = @s;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@s", state.ToString());
        var list = new List<ShipmentLineStateRecord>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    private static ShipmentLineStateRecord Map(SqlDataReader r) => new()
    {
        OrderLineId = r.GetInt64(0),
        CurrentState = Enum.Parse<ShipmentState>(r.GetString(1)),
        PreviousState = r.IsDBNull(2) ? null : Enum.Parse<ShipmentState>(r.GetString(2)),
        PickedCartons = r.GetInt32(3),
        ReceivedCartons = r.GetInt32(4),
        LastEventId = r.IsDBNull(5) ? null : r.GetInt64(5),
        StateEnteredUtc = new DateTimeOffset(r.GetDateTime(6), TimeSpan.Zero)
    };
}

/// <summary>SQL-backed exception store over ops.Exception.</summary>
public sealed class SqlExceptionStore : IExceptionStore
{
    private readonly string _cs;
    public SqlExceptionStore(string cs) => _cs = cs;

    public async Task AddAsync(TrackException ex, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
INSERT INTO ops.Exception
    (ExceptionType, Severity, Status, CheckpointId, OrderLineId, CartonId, TrayId, TripId, StoreId,
     TriggeringEventId, Detail, FrameBlobUri, CreatedUtc)
VALUES
    (@type, @sev, 'Open',
     (SELECT CheckpointId FROM ref.[Checkpoint] WHERE CheckpointCode = @cp),
     @ol, @carton, @tray, @trip, @store, @evt, @detail, @frame, @created);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", ex.Type.ToString());
        cmd.Parameters.AddWithValue("@sev", ex.Severity.ToString());
        cmd.Parameters.AddWithValue("@cp", (object?)ex.Checkpoint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ol", (object?)ex.OrderLineId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@carton", (object?)ex.CartonId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tray", (object?)ex.TrayId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@trip", (object?)ex.TripId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@store", (object?)ex.StoreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@evt", (object?)ex.TriggeringEventId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@detail", (object?)ex.Detail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@frame", (object?)ex.FrameBlobUri ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", ex.CreatedUtc.UtcDateTime);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TrackException>> GetOpenAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT e.ExceptionType, e.Severity, cp.CheckpointCode, e.OrderLineId, e.CartonId, e.TrayId, e.TripId,
       e.StoreId, e.TriggeringEventId, e.Detail, e.FrameBlobUri, e.CreatedUtc
FROM ops.Exception e LEFT JOIN ref.[Checkpoint] cp ON cp.CheckpointId = e.CheckpointId
WHERE e.Status IN ('Open','Acknowledged','Escalated') ORDER BY e.CreatedUtc DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        var list = new List<TrackException>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new TrackException
            {
                Type = Enum.Parse<ExceptionType>(r.GetString(0)),
                Severity = Enum.Parse<ExceptionSeverity>(r.GetString(1)),
                Checkpoint = r.IsDBNull(2) ? null : r.GetString(2),
                OrderLineId = r.IsDBNull(3) ? null : r.GetInt64(3),
                CartonId = r.IsDBNull(4) ? null : r.GetInt64(4),
                TrayId = r.IsDBNull(5) ? null : r.GetInt32(5),
                TripId = r.IsDBNull(6) ? null : r.GetInt64(6),
                StoreId = r.IsDBNull(7) ? null : r.GetInt32(7),
                TriggeringEventId = r.IsDBNull(8) ? null : r.GetInt64(8),
                Detail = r.IsDBNull(9) ? "" : r.GetString(9),
                FrameBlobUri = r.IsDBNull(10) ? null : r.GetString(10),
                CreatedUtc = new DateTimeOffset(r.GetDateTime(11), TimeSpan.Zero)
            });
        return list;
    }
}

/// <summary>SQL-backed manifest cache over ops.TrayManifest (migration 001).</summary>
public sealed class SqlManifestStore : IManifestStore
{
    private readonly string _cs;
    public SqlManifestStore(string cs) => _cs = cs;

    public async Task UpsertAsync(TrayManifest m, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.TrayManifest AS t
USING (SELECT @tray AS TrayQr) AS s ON t.TrayQr = s.TrayQr
WHEN MATCHED THEN UPDATE SET TripId=@trip, ExpectedCartonCount=@count, ExpectedPayloadsJson=@json, UpdatedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TrayQr, TripId, ExpectedCartonCount, ExpectedPayloadsJson, UpdatedUtc)
    VALUES (@tray, @trip, @count, @json, SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tray", m.TrayQr);
        cmd.Parameters.AddWithValue("@trip", (object?)m.TripId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@count", m.ExpectedCartonCount);
        cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(m.ExpectedCartonPayloads));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<TrayManifest?> GetAsync(string trayQr, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"SELECT TrayQr, TripId, ExpectedCartonCount, ExpectedPayloadsJson, UpdatedUtc
                             FROM ops.TrayManifest WHERE TrayQr = @tray;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tray", trayQr);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<IReadOnlyList<TrayManifest>> GetChangedSinceAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"SELECT TrayQr, TripId, ExpectedCartonCount, ExpectedPayloadsJson, UpdatedUtc
                             FROM ops.TrayManifest WHERE UpdatedUtc >= @since ORDER BY UpdatedUtc;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@since", since.UtcDateTime);
        var list = new List<TrayManifest>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    private static TrayManifest Map(SqlDataReader r) => new()
    {
        TrayQr = r.GetString(0),
        TripId = r.IsDBNull(1) ? null : r.GetInt64(1),
        ExpectedCartonCount = r.GetInt32(2),
        ExpectedCartonPayloads = r.IsDBNull(3) ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<List<string>>(r.GetString(3)) ?? new()),
        UpdatedUtc = new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero)
    };
}
