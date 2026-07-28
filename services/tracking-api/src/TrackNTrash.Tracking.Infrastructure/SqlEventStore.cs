using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// SQL-backed append-only event store against the Module 1 schema (ops.ScanEvent).
/// The device is upserted by code so the idempotency unique index UQ_ScanEvent_Idem
/// (DeviceId, ClientEventId) can dedupe a redelivered event to a no-op returning the
/// original row.
/// </summary>
public sealed class SqlEventStore : IEventStore
{
    private readonly string _cs;
    public SqlEventStore(string connectionString) => _cs = connectionString;

    public async Task<(StoredScanEvent Event, bool Duplicate)> AppendOrGetAsync(ScanEventInput input, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        const string sql = @"
-- Ensure a device row exists for this device code.
DECLARE @deviceId INT = (SELECT DeviceId FROM ops.Device WHERE DeviceCode = @deviceCode);
IF @deviceId IS NULL
BEGIN
    INSERT INTO ops.Device (DeviceCode, DeviceType) VALUES (@deviceCode, 'Api');
    SET @deviceId = SCOPE_IDENTITY();
END

-- Idempotent on (DeviceId, ClientEventId).
DECLARE @existing BIGINT = (SELECT ScanEventId FROM ops.ScanEvent WHERE DeviceId = @deviceId AND ClientEventId = @cid);
IF @existing IS NOT NULL
    SELECT @existing AS ScanEventId, CAST(1 AS bit) AS Duplicate;
ELSE
BEGIN
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
        cmd.Parameters.AddWithValue("@deviceCode", string.IsNullOrWhiteSpace(input.DeviceId) ? "unknown" : input.DeviceId);
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

/// <summary>
/// Order intake: creates SalesOrder + OrderLine (+ ShipmentLineState) rows so downstream SQL
/// stores satisfy their foreign keys. Backs POST /orders (also the D365 outbound target).
/// </summary>
public sealed class SqlOrderStore
{
    private readonly string _cs;
    public SqlOrderStore(string cs) => _cs = cs;

    public sealed record OrderLineInput(int LineNumber, string Gtin, decimal OrderedQty, string Uom, int ExpectedCartonCount, string? ErpLineReference);
    public sealed record OrderInput(string OrderNumber, string StoreCode, string? ErpReference, IReadOnlyList<OrderLineInput> Lines);

    /// <summary>Idempotent by OrderNumber. Returns the created/updated order line ids.</summary>
    public async Task<IReadOnlyList<long>> CreateAsync(OrderInput order, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        const string head = @"
-- Upsert store by code.
DECLARE @storeId INT = (SELECT StoreId FROM ops.Store WHERE StoreCode = @storeCode);
IF @storeId IS NULL
BEGIN
    INSERT INTO ops.Store (StoreCode, Name) VALUES (@storeCode, @storeCode);
    SET @storeId = SCOPE_IDENTITY();
END
-- Upsert sales order by number.
DECLARE @soId BIGINT = (SELECT SalesOrderId FROM ops.SalesOrder WHERE OrderNumber = @orderNumber);
IF @soId IS NULL
BEGIN
    INSERT INTO ops.SalesOrder (OrderNumber, StoreId, ErpReference) VALUES (@orderNumber, @storeId, @erp);
    SET @soId = SCOPE_IDENTITY();
END
SELECT @soId;";
        await using var headCmd = new SqlCommand(head, conn);
        headCmd.Parameters.AddWithValue("@storeCode", order.StoreCode);
        headCmd.Parameters.AddWithValue("@orderNumber", order.OrderNumber);
        headCmd.Parameters.AddWithValue("@erp", (object?)order.ErpReference ?? DBNull.Value);
        var soId = (long)(await headCmd.ExecuteScalarAsync(ct))!;

        var lineIds = new List<long>();
        foreach (var line in order.Lines)
        {
            const string lineSql = @"
DECLARE @olId BIGINT = (SELECT OrderLineId FROM ops.OrderLine WHERE SalesOrderId=@so AND LineNumber=@ln);
IF @olId IS NULL
BEGIN
    INSERT INTO ops.OrderLine (SalesOrderId, LineNumber, Gtin, OrderedQty, Uom, ExpectedCartonCount, ErpLineReference)
    VALUES (@so, @ln, @gtin, @qty, @uom, @exp, @erpl);
    SET @olId = SCOPE_IDENTITY();
    INSERT INTO ops.ShipmentLineState (OrderLineId, CurrentState) VALUES (@olId, 'Ordered');
END
SELECT @olId;";
            await using var lineCmd = new SqlCommand(lineSql, conn);
            lineCmd.Parameters.AddWithValue("@so", soId);
            lineCmd.Parameters.AddWithValue("@ln", line.LineNumber);
            lineCmd.Parameters.AddWithValue("@gtin", line.Gtin);
            lineCmd.Parameters.AddWithValue("@qty", line.OrderedQty);
            lineCmd.Parameters.AddWithValue("@uom", string.IsNullOrWhiteSpace(line.Uom) ? "EA" : line.Uom);
            lineCmd.Parameters.AddWithValue("@exp", line.ExpectedCartonCount);
            lineCmd.Parameters.AddWithValue("@erpl", (object?)line.ErpLineReference ?? DBNull.Value);
            lineIds.Add((long)(await lineCmd.ExecuteScalarAsync(ct))!);
        }
        return lineIds;
    }
}
