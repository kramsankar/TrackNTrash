using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Item-level tracking: the units inside a carton. Two identification modes coexist —
/// barcoded units are scanned individually (ops.Item rows), unlabelled units are counted
/// visually by a camera. Both land in ops.ItemCount so the same reconciliation applies
/// at every checkpoint (pick, dock, receiving).
/// </summary>
public sealed class SqlItemStore
{
    private readonly string _cs;
    public SqlItemStore(string cs) => _cs = cs;

    // ---------- carton setup ----------

    /// <summary>Defines what a carton should contain and how its units are identified.</summary>
    public async Task SetCartonExpectationAsync(long cartonId, int expectedItems, string identification, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"UPDATE ops.Carton SET ExpectedItemCount = @n, ItemIdentification = @mode WHERE CartonId = @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", expectedItems);
        cmd.Parameters.AddWithValue("@mode", identification);
        cmd.Parameters.AddWithValue("@id", cartonId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Registers the barcoded units expected in a carton.</summary>
    public async Task<int> AddItemsAsync(long cartonId, IEnumerable<(string Barcode, string? Gtin, string? Description)> items, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        int added = 0;
        foreach (var (barcode, gtin, desc) in items)
        {
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM ops.Item WHERE CartonId=@c AND Barcode=@b)
BEGIN
    INSERT INTO ops.Item (CartonId, Barcode, Gtin, Description) VALUES (@c, @b, @g, @d);
    SELECT 1;
END
ELSE SELECT 0;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@c", cartonId);
            cmd.Parameters.AddWithValue("@b", barcode);
            cmd.Parameters.AddWithValue("@g", (object?)gtin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)desc ?? DBNull.Value);
            added += (int)(await cmd.ExecuteScalarAsync(ct))!;
        }
        return added;
    }

    // ---------- observation / reconciliation ----------

    public sealed record CountResult(long ItemCountId, int Expected, int Scanned, int? Vision, string Verdict, string Detail);

    /// <summary>
    /// Records an observation of a carton's contents at a checkpoint and reconciles it.
    ///
    /// <paramref name="scannedBarcodes"/> — barcoded units actually scanned (may be empty).
    /// <paramref name="visionCount"/>     — units a camera counted (null when no camera looked).
    ///
    /// The effective count is the greater of the two signals when both are present: a scan
    /// proves identity, a camera sees units that were never scanned. Disagreement between the
    /// two is itself reported, because it usually means an unlabelled or unreadable unit.
    /// </summary>
    public async Task<CountResult> RecordCountAsync(
        long cartonId, string? checkpointCode, IReadOnlyList<string> scannedBarcodes,
        int? visionCount, int? cameraId, string? frameBlobUri, decimal? confidence,
        long? scanEventId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Expected units for this carton.
        int expected;
        await using (var e = new SqlCommand("SELECT ExpectedItemCount FROM ops.Carton WHERE CartonId=@id;", conn))
        {
            e.Parameters.AddWithValue("@id", cartonId);
            var raw = await e.ExecuteScalarAsync(ct);
            if (raw is null) throw new ArgumentException($"Carton {cartonId} not found.");
            expected = Convert.ToInt32(raw);
        }

        // Mark scanned items as verified; count only those that genuinely belong to the carton.
        int scanned = 0, unexpected = 0;
        foreach (var barcode in scannedBarcodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            const string sql = @"
UPDATE ops.Item SET Status='Verified' WHERE CartonId=@c AND Barcode=@b;
SELECT @@ROWCOUNT;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@c", cartonId);
            cmd.Parameters.AddWithValue("@b", barcode);
            var hit = (int)(await cmd.ExecuteScalarAsync(ct))!;
            if (hit > 0) scanned++; else unexpected++;
        }

        // Effective observed count: trust the strongest evidence available.
        int observed = visionCount.HasValue ? Math.Max(scanned, visionCount.Value) : scanned;
        bool anyEvidence = scannedBarcodes.Count > 0 || visionCount.HasValue;

        string verdict = !anyEvidence ? "UNVERIFIED"
            : observed == expected ? "MATCH"
            : observed < expected ? "SHORT" : "OVER";

        var notes = new List<string> { $"expected {expected}" };
        if (scannedBarcodes.Count > 0) notes.Add($"scanned {scanned}");
        if (visionCount.HasValue) notes.Add($"vision {visionCount}");
        if (unexpected > 0) notes.Add($"{unexpected} not on this carton");
        if (visionCount.HasValue && scannedBarcodes.Count > 0 && visionCount.Value != scanned)
            notes.Add($"scan/vision disagree by {Math.Abs(visionCount.Value - scanned)}");
        var detail = string.Join(", ", notes);

        const string ins = @"
INSERT INTO ops.ItemCount (CartonId, CheckpointId, ExpectedCount, ScannedCount, VisionCount,
                           CameraId, Verdict, FrameBlobUri, Confidence, ScanEventId)
VALUES (@c, (SELECT CheckpointId FROM ref.[Checkpoint] WHERE CheckpointCode=@cp),
        @exp, @scan, @vis, @cam, @verdict, @frame, @conf, @evt);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";
        await using var insCmd = new SqlCommand(ins, conn);
        insCmd.Parameters.AddWithValue("@c", cartonId);
        insCmd.Parameters.AddWithValue("@cp", (object?)checkpointCode ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("@exp", expected);
        insCmd.Parameters.AddWithValue("@scan", scanned);
        insCmd.Parameters.AddWithValue("@vis", (object?)visionCount ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("@cam", (object?)cameraId ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("@verdict", verdict);
        insCmd.Parameters.AddWithValue("@frame", (object?)frameBlobUri ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("@conf", (object?)confidence ?? DBNull.Value);
        insCmd.Parameters.AddWithValue("@evt", (object?)scanEventId ?? DBNull.Value);
        var id = (long)(await insCmd.ExecuteScalarAsync(ct))!;

        return new CountResult(id, expected, scanned, visionCount, verdict, detail);
    }

    // ---------- reads ----------

    public sealed record ItemCountRow(long ItemCountId, long CartonId, string CartonSerial, string? Checkpoint,
        int ExpectedCount, int ScannedCount, int? VisionCount, string? CameraCode, string Verdict,
        decimal? Confidence, DateTimeOffset ObservedUtc);

    public async Task<IReadOnlyList<ItemCountRow>> ListCountsAsync(int top = 500, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT TOP (@top) ic.ItemCountId, ic.CartonId, c.Serial, cp.CheckpointCode,
       ic.ExpectedCount, ic.ScannedCount, ic.VisionCount, cam.CameraCode,
       ic.Verdict, ic.Confidence, ic.ObservedUtc
FROM ops.ItemCount ic
JOIN ops.Carton c ON c.CartonId = ic.CartonId
LEFT JOIN ref.[Checkpoint] cp ON cp.CheckpointId = ic.CheckpointId
LEFT JOIN ops.Camera cam ON cam.CameraId = ic.CameraId
ORDER BY ic.ItemCountId DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@top", top);
        var list = new List<ItemCountRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ItemCountRow(
                r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
                r.GetInt32(4), r.GetInt32(5), r.IsDBNull(6) ? null : r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8),
                r.IsDBNull(9) ? null : r.GetDecimal(9),
                new DateTimeOffset(r.GetDateTime(10), TimeSpan.Zero)));
        return list;
    }

    public sealed record CartonRow(long CartonId, string Serial, string Gtin, int ExpectedItemCount,
        string ItemIdentification, int RegisteredItems, string Status);

    public async Task<IReadOnlyList<CartonRow>> ListCartonsAsync(int top = 500, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT TOP (@top) c.CartonId, c.Serial, c.Gtin, c.ExpectedItemCount, c.ItemIdentification,
       (SELECT COUNT(*) FROM ops.Item i WHERE i.CartonId = c.CartonId) AS RegisteredItems, c.Status
FROM ops.Carton c ORDER BY c.CartonId DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@top", top);
        var list = new List<CartonRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CartonRow(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                r.GetString(4), r.GetInt32(5), r.GetString(6)));
        return list;
    }

    /// <summary>Creates a carton directly (for item-level demos and manual entry).</summary>
    public async Task<long> CreateCartonAsync(long orderLineId, string gtin, string serial,
        int expectedItems, string identification, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
DECLARE @id BIGINT = (SELECT CartonId FROM ops.Carton WHERE Gtin=@g AND Serial=@s);
IF @id IS NULL
BEGIN
    INSERT INTO ops.Carton (OrderLineId, Gtin, Serial, QrPayload, ExpectedItemCount, ItemIdentification)
    VALUES (@ol, @g, @s, '01' + @g + '21' + @s, @n, @mode);
    SET @id = SCOPE_IDENTITY();
END
ELSE UPDATE ops.Carton SET ExpectedItemCount=@n, ItemIdentification=@mode WHERE CartonId=@id;
SELECT @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ol", orderLineId);
        cmd.Parameters.AddWithValue("@g", gtin);
        cmd.Parameters.AddWithValue("@s", serial);
        cmd.Parameters.AddWithValue("@n", expectedItems);
        cmd.Parameters.AddWithValue("@mode", identification);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
