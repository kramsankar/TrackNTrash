using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core.Receiving;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// SQL-backed ASN store over ops.Asn / ops.AsnLine (migration 007).
///
/// ASNs used to live in memory, which meant an App Service recycle left a store unable to
/// receive a tray already on its way: no expected-carton list, so every scan read as an
/// over-scan.
/// </summary>
public sealed class SqlAsnStore : IAsnStore
{
    private readonly string _cs;
    public SqlAsnStore(string cs) => _cs = cs;

    public async Task UpsertAsync(Asn asn, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        // Lines are replaced wholesale: an ASN is restated by the sender, never patched.
        const string header = @"
MERGE ops.Asn WITH (HOLDLOCK) AS t
USING (SELECT @qr AS TrayQr, @store AS StoreCode) AS s
   ON t.TrayQr = s.TrayQr AND t.StoreCode = s.StoreCode
WHEN MATCHED THEN UPDATE SET UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (TrayQr, StoreCode) VALUES (s.TrayQr, s.StoreCode);
SELECT AsnId FROM ops.Asn WHERE TrayQr = @qr AND StoreCode = @store;";
        int asnId;
        await using (var cmd = new SqlCommand(header, conn, tx))
        {
            cmd.Parameters.AddWithValue("@qr", asn.TrayQr);
            cmd.Parameters.AddWithValue("@store", asn.StoreCode);
            asnId = (int)(await cmd.ExecuteScalarAsync(ct))!;
        }

        await using (var del = new SqlCommand("DELETE FROM ops.AsnLine WHERE AsnId = @id;", conn, tx))
        {
            del.Parameters.AddWithValue("@id", asnId);
            await del.ExecuteNonQueryAsync(ct);
        }

        foreach (var line in asn.ExpectedCartons)
        {
            await using var ins = new SqlCommand(
                "INSERT INTO ops.AsnLine (AsnId, Payload, OrderLineId, Gtin) VALUES (@id, @p, @ol, @g);", conn, tx);
            ins.Parameters.AddWithValue("@id", asnId);
            ins.Parameters.AddWithValue("@p", line.Payload);
            ins.Parameters.AddWithValue("@ol", line.OrderLineId);
            ins.Parameters.AddWithValue("@g", (object?)line.Gtin ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<Asn?> GetAsync(string trayQr, string storeCode, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT l.Payload, l.OrderLineId, l.Gtin
FROM ops.Asn a LEFT JOIN ops.AsnLine l ON l.AsnId = a.AsnId
WHERE a.TrayQr = @qr AND a.StoreCode = @store
ORDER BY l.AsnLineId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qr", trayQr);
        cmd.Parameters.AddWithValue("@store", storeCode);

        var lines = new List<ExpectedCarton>();
        var found = false;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            found = true;
            // LEFT JOIN: an ASN with no lines yet still exists, and must not read as missing.
            if (r.IsDBNull(0)) continue;
            lines.Add(new ExpectedCarton
            {
                Payload = r.GetString(0),
                OrderLineId = r.GetInt64(1),
                Gtin = r.IsDBNull(2) ? null : r.GetString(2)
            });
        }
        if (!found) return null;
        return new Asn { TrayQr = trayQr, StoreCode = storeCode, ExpectedCartons = lines };
    }

    public async Task<string?> FindStoreForCartonAsync(string payload, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 a.StoreCode
FROM ops.AsnLine l JOIN ops.Asn a ON a.AsnId = l.AsnId
WHERE l.Payload = @p ORDER BY l.AsnLineId;", conn);
        cmd.Parameters.AddWithValue("@p", payload);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }
}
