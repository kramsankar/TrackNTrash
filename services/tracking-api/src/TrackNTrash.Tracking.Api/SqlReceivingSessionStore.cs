using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core.Receiving;

namespace TrackNTrash.Tracking.Api;

/// <summary>
/// Receiving sessions over ops.ReceivingSession / ops.ReceivingSessionScan (migration 008).
///
/// The session is the state of a colleague working through a tray at the store door. Held
/// in a dictionary it did not survive a recycle, so the tray had to be restarted from the
/// first carton — and because the id counter restarted too, a retried request could land
/// on a different tray's session. Ids now come from a sequence and are never reused.
///
/// The expected-carton list is not copied here; it is read back from the ASN, which is the
/// record of what the tray should hold.
/// </summary>
public sealed class SqlReceivingSessionStore : IReceivingSessionStore
{
    private readonly string _cs;
    private readonly IAsnStore _asns;

    public SqlReceivingSessionStore(string cs, IAsnStore asns)
    {
        _cs = cs;
        _asns = asns;
    }

    public async Task<string> AddAsync(ReceivingSession session, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
DECLARE @n BIGINT = NEXT VALUE FOR ops.ReceivingSessionSeq;
DECLARE @sid NVARCHAR(40) = 'recv-' + RIGHT('000000' + CAST(@n AS NVARCHAR(20)), 6);
INSERT INTO ops.ReceivingSession (SessionId, TrayQr, StoreCode) VALUES (@sid, @qr, @store);
SELECT @sid;", conn);
        cmd.Parameters.AddWithValue("@qr", session.Asn.TrayQr);
        cmd.Parameters.AddWithValue("@store", session.Asn.StoreCode);
        var id = (string)(await cmd.ExecuteScalarAsync(ct))!;

        // A session started from a partially-scanned state would otherwise lose those scans.
        await SaveAsync(id, session, ct);
        return id;
    }

    public async Task<ReceivingSession?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        string trayQr, storeCode;
        await using (var head = new SqlCommand(
            "SELECT TrayQr, StoreCode FROM ops.ReceivingSession WHERE SessionId = @sid;", conn))
        {
            head.Parameters.AddWithValue("@sid", id);
            await using var hr = await head.ExecuteReaderAsync(ct);
            if (!await hr.ReadAsync(ct)) return null;
            trayQr = hr.GetString(0);
            storeCode = hr.GetString(1);
        }

        // Without the ASN there is no expected list, so the session cannot be resumed
        // meaningfully — treat it as gone rather than resuming against an empty tray.
        var asn = await _asns.GetAsync(trayQr, storeCode, ct);
        if (asn is null) return null;

        var session = new ReceivingSession { Asn = asn };
        await using (var scans = new SqlCommand(@"
SELECT s.Payload, s.Outcome
FROM ops.ReceivingSessionScan s
JOIN ops.ReceivingSession h ON h.ReceivingSessionId = s.ReceivingSessionId
WHERE h.SessionId = @sid ORDER BY s.ReceivingSessionScanId;", conn))
        {
            scans.Parameters.AddWithValue("@sid", id);
            await using var sr = await scans.ExecuteReaderAsync(ct);
            while (await sr.ReadAsync(ct))
            {
                var payload = sr.GetString(0);
                switch (sr.GetString(1))
                {
                    case "Received": session.Received.Add(payload); break;
                    case "Over": session.Over.Add(payload); break;
                    case "Damaged": session.Damaged.Add(payload); break;
                }
            }
        }
        return session;
    }

    public async Task SaveAsync(string id, ReceivingSession session, CancellationToken ct = default)
    {
        // Damaged wins over Received: a carton that arrived broken is not a clean receipt.
        var rows = session.Received.Select(p => (p, "Received"))
            .Concat(session.Over.Select(p => (p, "Over")))
            .Concat(session.Damaged.Select(p => (p, "Damaged")))
            .GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Item2 == "Damaged" ? 0 : 1).First())
            .ToList();
        if (rows.Count == 0) return;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        foreach (var (payload, outcome) in rows)
        {
            await using var cmd = new SqlCommand(@"
DECLARE @rid BIGINT = (SELECT ReceivingSessionId FROM ops.ReceivingSession WHERE SessionId = @sid);
IF @rid IS NULL RETURN;

MERGE ops.ReceivingSessionScan WITH (HOLDLOCK) AS t
USING (SELECT @rid AS ReceivingSessionId, @p AS Payload) AS s
   ON t.ReceivingSessionId = s.ReceivingSessionId AND t.Payload = s.Payload
WHEN MATCHED AND t.Outcome <> @o THEN UPDATE SET Outcome = @o
WHEN NOT MATCHED THEN INSERT (ReceivingSessionId, Payload, Outcome)
                      VALUES (s.ReceivingSessionId, s.Payload, @o);

UPDATE ops.ReceivingSession SET UpdatedUtc = SYSUTCDATETIME() WHERE ReceivingSessionId = @rid;", conn);
            cmd.Parameters.AddWithValue("@sid", id);
            cmd.Parameters.AddWithValue("@p", payload);
            cmd.Parameters.AddWithValue("@o", outcome);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        // Scans cascade. The outcome of the round lives in ops.ScanEvent, not here.
        await using var cmd = new SqlCommand("DELETE FROM ops.ReceivingSession WHERE SessionId = @sid;", conn);
        cmd.Parameters.AddWithValue("@sid", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
