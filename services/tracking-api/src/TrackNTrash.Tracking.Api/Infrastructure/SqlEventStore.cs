using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Api.Infrastructure;

/// <summary>
/// SQL-backed append-only event store against the Module 1 schema (ops.ScanEvent).
/// Representative production implementation; idempotency is enforced by the unique index
/// UQ_ScanEvent_Idem (DeviceId, ClientEventId) — a duplicate insert is caught and the
/// existing row returned. (ShipmentState / Exception / Manifest SQL stores follow the same
/// pattern; in-memory versions are used for local/dev and tests.)
/// </summary>
public sealed class SqlEventStore : IEventStore
{
    private readonly string _cs;
    public SqlEventStore(string connectionString) => _cs = connectionString;

    public async Task<(StoredScanEvent Event, bool Duplicate)> AppendOrGetAsync(ScanEventInput input, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Resolve DeviceId (int) from device code is handled upstream; here we assume input carries it
        // via convention. For brevity we store the raw device string and dedupe on (deviceCode, clientEventId)
        // using a helper column. In the full schema DeviceId is an int FK; adapt as needed.
        const string sql = @"
IF EXISTS (SELECT 1 FROM ops.ScanEvent e
           JOIN ops.Device d ON d.DeviceId = e.DeviceId
           WHERE d.DeviceCode = @deviceCode AND e.ClientEventId = @cid)
BEGIN
    SELECT e.ScanEventId, CAST(1 AS bit) AS Duplicate
    FROM ops.ScanEvent e JOIN ops.Device d ON d.DeviceId = e.DeviceId
    WHERE d.DeviceCode = @deviceCode AND e.ClientEventId = @cid;
END
ELSE
BEGIN
    DECLARE @deviceId INT = (SELECT DeviceId FROM ops.Device WHERE DeviceCode = @deviceCode);
    INSERT INTO ops.ScanEvent
        (EventType, CheckpointId, DeviceId, UserId, ClientEventId, ScannedQr,
         OrderLineId, CartonId, TrayId, StoreId, TripId, Verdict, PayloadJson, EventUtc)
    VALUES
        (@eventType,
         (SELECT CheckpointId FROM ref.[Checkpoint] WHERE CheckpointCode = @checkpoint),
         @deviceId, @userId, @cid, @scannedQr,
         @orderLineId, @cartonId, @trayId, @storeId, @tripId, @verdict, @payload, @eventUtc);
    SELECT CAST(SCOPE_IDENTITY() AS bigint) AS ScanEventId, CAST(0 AS bit) AS Duplicate;
END";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@deviceCode", input.DeviceId);
        cmd.Parameters.AddWithValue("@cid", input.ClientEventId);
        cmd.Parameters.AddWithValue("@eventType", input.EventType);
        cmd.Parameters.AddWithValue("@checkpoint", (object?)input.Checkpoint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userId", (object?)input.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@scannedQr", (object?)input.ScannedQr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderLineId", (object?)input.OrderLineId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cartonId", (object?)input.CartonId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@trayId", (object?)input.TrayId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@storeId", (object?)input.StoreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tripId", (object?)input.TripId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@verdict", (object?)input.Verdict ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@payload", (object?)input.MetaJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@eventUtc", input.EventUtc.UtcDateTime);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var dup = reader.GetBoolean(1);
            return (new StoredScanEvent { ScanEventId = id, Input = input }, dup);
        }
        throw new InvalidOperationException("Event insert returned no row.");
    }

    public async Task<IReadOnlyList<StoredScanEvent>> GetByOrderLineAsync(long orderLineId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"SELECT ScanEventId, EventType, ClientEventId, ScannedQr, Verdict, EventUtc
                             FROM ops.ScanEvent WHERE OrderLineId = @ol ORDER BY ScanEventId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ol", orderLineId);
        var list = new List<StoredScanEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new StoredScanEvent
            {
                ScanEventId = reader.GetInt64(0),
                Input = new ScanEventInput
                {
                    EventType = reader.GetString(1),
                    ClientEventId = reader.GetString(2),
                    ScannedQr = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Verdict = reader.IsDBNull(4) ? null : reader.GetString(4),
                    OrderLineId = orderLineId,
                    EventUtc = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero)
                }
            });
        }
        return list;
    }
}
