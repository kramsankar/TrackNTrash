using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core;

namespace TrackNTrash.Tracking.Api.Console;

/// <summary>
/// Console read/action model over ops.Exception + ops.ExceptionAudit (migration 007).
///
/// The console previously read a private in-memory list, so a restart showed an empty
/// board while ops.Exception still held everything unactioned — and the ids on screen
/// were a local counter that matched no row in the database.
/// </summary>
public sealed class SqlConsoleExceptionStore : IConsoleExceptionStore
{
    private readonly string _cs;
    public SqlConsoleExceptionStore(string cs) => _cs = cs;

    private const string SelectList = @"
SELECT e.ExceptionId, e.ExceptionType, e.Severity, e.Status, cp.CheckpointCode,
       e.OrderLineId, e.TrayId, e.TripId, e.StoreId, e.Detail, e.FrameBlobUri,
       e.PhotoBlobUri, e.CreatedUtc
FROM ops.Exception e LEFT JOIN ref.[Checkpoint] cp ON cp.CheckpointId = e.CheckpointId";

    private static ConsoleException Read(SqlDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Type = r.GetString(1),
        Severity = r.GetString(2),
        Status = r.IsDBNull(3) ? "Open" : r.GetString(3),
        Checkpoint = r.IsDBNull(4) ? null : r.GetString(4),
        OrderLineId = r.IsDBNull(5) ? null : r.GetInt64(5),
        TrayId = r.IsDBNull(6) ? null : r.GetInt32(6),
        TripId = r.IsDBNull(7) ? null : r.GetInt64(7),
        StoreId = r.IsDBNull(8) ? null : r.GetInt32(8),
        Detail = r.IsDBNull(9) ? "" : r.GetString(9),
        FrameBlobUri = r.IsDBNull(10) ? null : r.GetString(10),
        PhotoBlobUri = r.IsDBNull(11) ? null : r.GetString(11),
        CreatedUtc = new DateTimeOffset(r.GetDateTime(12), TimeSpan.Zero)
    };

    /// <summary>
    /// The exception row is written by IExceptionStore before the notification is published,
    /// so this resolves the id that was just assigned rather than inserting a second copy.
    /// </summary>
    public async Task<ConsoleException> AddAsync(TrackException ex, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 ExceptionId FROM ops.Exception
WHERE ExceptionType = @type
  AND ISNULL(OrderLineId, -1) = ISNULL(@ol, -1)
  AND CreatedUtc >= DATEADD(second, -30, @created)
ORDER BY ExceptionId DESC;", conn);
        cmd.Parameters.AddWithValue("@type", ex.Type.ToString());
        cmd.Parameters.AddWithValue("@ol", (object?)ex.OrderLineId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", ex.CreatedUtc.UtcDateTime);
        var id = await cmd.ExecuteScalarAsync(ct);

        // Id 0 means the lookup missed; the console still gets the live push, it just
        // cannot be actioned until the list is refreshed from SQL.
        return new ConsoleException
        {
            Id = id is null or DBNull ? 0 : Convert.ToInt64(id),
            Type = ex.Type.ToString(),
            Severity = ex.Severity.ToString(),
            Status = "Open",
            Checkpoint = ex.Checkpoint,
            OrderLineId = ex.OrderLineId,
            TrayId = ex.TrayId,
            TripId = ex.TripId,
            StoreId = ex.StoreId,
            Detail = ex.Detail,
            FrameBlobUri = ex.FrameBlobUri,
            CreatedUtc = ex.CreatedUtc
        };
    }

    public async Task<IReadOnlyList<ConsoleException>> ListAsync(string? checkpoint, string? severity,
        string? status, string? route, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Route has no column on ops.Exception; it is accepted for API compatibility and
        // filters nothing, exactly as it did in the in-memory store.
        var sql = SelectList + @"
WHERE (@cp IS NULL OR cp.CheckpointCode = @cp)
  AND (@sev IS NULL OR e.Severity = @sev)
  AND (@st IS NULL OR e.Status = @st)
ORDER BY e.CreatedUtc DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cp", (object?)checkpoint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sev", (object?)severity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@st", (object?)status ?? DBNull.Value);

        var list = new List<ConsoleException>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Read(r));
        return list;
    }

    public async Task<ConsoleException?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(SelectList + " WHERE e.ExceptionId = @id;", conn);
        cmd.Parameters.AddWithValue("@id", id);

        ConsoleException? found = null;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            if (await r.ReadAsync(ct)) found = Read(r);
        if (found is null) return null;

        await using var audit = new SqlCommand(
            "SELECT Action, ActionedByUser, ActionedUtc, Note FROM ops.ExceptionAudit " +
            "WHERE ExceptionId = @id ORDER BY ExceptionAuditId;", conn);
        audit.Parameters.AddWithValue("@id", id);
        await using var ar = await audit.ExecuteReaderAsync(ct);
        while (await ar.ReadAsync(ct))
            found.Audit.Add(new AuditEntry(ar.GetString(0), ar.GetString(1),
                new DateTimeOffset(ar.GetDateTime(2), TimeSpan.Zero),
                ar.IsDBNull(3) ? null : ar.GetString(3)));
        return found;
    }

    public async Task<ConsoleException?> ApplyAsync(long id, string action, string user, string? note,
        CancellationToken ct = default)
    {
        var newStatus = ConsoleActions.StatusFor(action);
        if (newStatus is null) return await GetAsync(id, ct);

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        await using (var upd = new SqlCommand(@"
UPDATE ops.Exception
   SET Status = @st,
       AcknowledgedByUser = CASE WHEN @st = 'Acknowledged' THEN @user ELSE AcknowledgedByUser END,
       AcknowledgedUtc    = CASE WHEN @st = 'Acknowledged' THEN SYSUTCDATETIME() ELSE AcknowledgedUtc END,
       ResolvedByUser     = CASE WHEN @st = 'Resolved' THEN @user ELSE ResolvedByUser END,
       ResolvedUtc        = CASE WHEN @st = 'Resolved' THEN SYSUTCDATETIME() ELSE ResolvedUtc END,
       ResolutionNote     = CASE WHEN @st = 'Resolved' THEN @note ELSE ResolutionNote END
 WHERE ExceptionId = @id;", conn, tx))
        {
            upd.Parameters.AddWithValue("@st", newStatus);
            upd.Parameters.AddWithValue("@user", user);
            upd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
            upd.Parameters.AddWithValue("@id", id);
            if (await upd.ExecuteNonQueryAsync(ct) == 0)
            {
                await tx.RollbackAsync(ct);
                return null;
            }
        }

        await using (var ins = new SqlCommand(
            "INSERT INTO ops.ExceptionAudit (ExceptionId, Action, ActionedByUser, Note) " +
            "VALUES (@id, @a, @user, @note);", conn, tx))
        {
            ins.Parameters.AddWithValue("@id", id);
            ins.Parameters.AddWithValue("@a", action);
            ins.Parameters.AddWithValue("@user", user);
            ins.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }
}
