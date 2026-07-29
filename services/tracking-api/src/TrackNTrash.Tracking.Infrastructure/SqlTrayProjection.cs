using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Writes the tray projections that the event stream implies: ops.TrayCustody (the
/// chain of who held a tray) and ops.TrayContent (which cartons were bound into it).
///
/// Both tables existed from the first schema but nothing ever wrote to them, so the
/// asset analytics that depend on custody — dwell time, loss rate, fleet sizing — had
/// no data behind them.
/// </summary>
public sealed class SqlTrayProjection : ITrayProjection
{
    private readonly string _cs;
    public SqlTrayProjection(string cs) => _cs = cs;

    public async Task RecordCustodyAsync(string trayQr, string toCustodianType, string? toCustodianRef,
        long? tripId, long? scanEventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trayQr)) return;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // The "from" side is whatever the tray currently reports, so the chain links up
        // without the caller having to know the previous holder.
        const string sql = @"
DECLARE @tid INT = (SELECT TrayId FROM ops.Tray WHERE TrayQr=@qr);
IF @tid IS NULL RETURN;

DECLARE @fromType VARCHAR(20), @fromRef NVARCHAR(40);
SELECT @fromType = CurrentCustodianType, @fromRef = CurrentCustodianRef
FROM ops.Tray WHERE TrayId=@tid;

-- An event that names no holder (a tray built in the warehouse it already sits in)
-- must not blank the one on record.
SET @toRef = ISNULL(@toRef, @fromRef);

-- Nothing to record when the tray has not actually moved.
IF (@fromType = @toType AND ISNULL(@fromRef,'') = ISNULL(@toRef,'')) RETURN;

INSERT INTO ops.TrayCustody (TrayId, FromCustodianType, FromCustodianRef,
                             ToCustodianType, ToCustodianRef, TripId, ScanEventId)
VALUES (@tid, @fromType, @fromRef, @toType, @toRef, @trip, @evt);

UPDATE ops.Tray
   SET CurrentCustodianType = @toType,
       CurrentCustodianRef  = @toRef,
       LastSeenUtc          = SYSUTCDATETIME(),
       TrayStatus = CASE @toType
                      WHEN 'Vehicle'  THEN 'InTransit'
                      WHEN 'Store'    THEN 'AtStore'
                      WHEN 'Warehouse' THEN 'Available'
                      ELSE TrayStatus END
 WHERE TrayId = @tid;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qr", trayQr);
        cmd.Parameters.AddWithValue("@toType", toCustodianType);
        cmd.Parameters.AddWithValue("@toRef", (object?)toCustodianRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@trip", (object?)tripId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@evt", (object?)scanEventId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task BindCartonsAsync(string trayQr, IReadOnlyList<string> cartonPayloads,
        long? scanEventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trayQr) || cartonPayloads.Count == 0) return;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        foreach (var payload in cartonPayloads.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // A carton may be bound to at most one tray at a time (UX_TrayContent_ActiveCarton),
            // so any live binding elsewhere is closed before the new one opens.
            const string sql = @"
DECLARE @tid INT = (SELECT TrayId FROM ops.Tray WHERE TrayQr=@qr);
DECLARE @cid BIGINT = (SELECT TOP 1 CartonId FROM ops.Carton
                       WHERE QrPayload=@payload OR Serial=@payload);
IF @tid IS NULL OR @cid IS NULL RETURN;

UPDATE ops.TrayContent SET UnboundUtc = SYSUTCDATETIME(), UnbindScanEventId = @evt
 WHERE CartonId = @cid AND UnboundUtc IS NULL AND TrayId <> @tid;

IF NOT EXISTS (SELECT 1 FROM ops.TrayContent
               WHERE CartonId=@cid AND TrayId=@tid AND UnboundUtc IS NULL)
BEGIN
    INSERT INTO ops.TrayContent (TrayId, CartonId, BindScanEventId) VALUES (@tid, @cid, @evt);
    UPDATE ops.Carton SET CurrentTrayId = @tid WHERE CartonId = @cid;
END";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@qr", trayQr);
            cmd.Parameters.AddWithValue("@payload", payload);
            cmd.Parameters.AddWithValue("@evt", (object?)scanEventId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
