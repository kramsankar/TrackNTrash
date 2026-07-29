using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Camera registry and floor-plan placement. Cameras carry both a structured location
/// (site → zone → station) and, optionally, an (x, y) pin on a site map. Coordinates are
/// 0..1 fractions so a placement renders correctly at any display size.
/// </summary>
public sealed class SqlCameraStore
{
    private readonly string _cs;
    public SqlCameraStore(string cs) => _cs = cs;

    public sealed record CameraRow(int CameraId, string CameraCode, string Name, string CameraKind,
        string SiteCode, string? Zone, string? Station, string? Checkpoint, string? RtspUrl,
        string Purpose, string Status, DateTimeOffset? LastSeenUtc,
        decimal? X, decimal? Y, int? HeadingDeg, int? SiteMapId);

    public async Task<IReadOnlyList<CameraRow>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT c.CameraId, c.CameraCode, c.Name, c.CameraKind, c.SiteCode, c.Zone, c.Station,
       cp.CheckpointCode, c.RtspUrl, c.Purpose, c.Status, c.LastSeenUtc,
       p.X, p.Y, p.HeadingDeg, p.SiteMapId
FROM ops.Camera c
LEFT JOIN ref.[Checkpoint] cp ON cp.CheckpointId = c.CheckpointId
LEFT JOIN ops.CameraPlacement p ON p.CameraId = c.CameraId
ORDER BY c.SiteCode, c.Zone, c.CameraCode;";
        await using var cmd = new SqlCommand(sql, conn);
        var list = new List<CameraRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CameraRow(
                r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8),
                r.GetString(9), r.GetString(10),
                r.IsDBNull(11) ? null : new DateTimeOffset(r.GetDateTime(11), TimeSpan.Zero),
                r.IsDBNull(12) ? null : r.GetDecimal(12), r.IsDBNull(13) ? null : r.GetDecimal(13),
                r.IsDBNull(14) ? null : r.GetInt32(14), r.IsDBNull(15) ? null : r.GetInt32(15)));
        return list;
    }

    /// <summary>Registers or updates a camera, keyed by its code.</summary>
    public async Task<int> UpsertAsync(string cameraCode, string name, string kind, string siteCode,
        string? zone, string? station, string? checkpointCode, string? rtspUrl, string purpose,
        string status, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.Camera AS t
USING (SELECT @code AS CameraCode) AS s ON t.CameraCode = s.CameraCode
WHEN MATCHED THEN UPDATE SET Name=@name, CameraKind=@kind, SiteCode=@site, Zone=@zone, Station=@station,
    CheckpointId=(SELECT CheckpointId FROM ref.[Checkpoint] WHERE CheckpointCode=@cp),
    RtspUrl=@rtsp, Purpose=@purpose, Status=@status
WHEN NOT MATCHED THEN INSERT (CameraCode, Name, CameraKind, SiteCode, Zone, Station, CheckpointId, RtspUrl, Purpose, Status)
    VALUES (@code, @name, @kind, @site, @zone, @station,
            (SELECT CheckpointId FROM ref.[Checkpoint] WHERE CheckpointCode=@cp), @rtsp, @purpose, @status);
SELECT CameraId FROM ops.Camera WHERE CameraCode = @code;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@code", cameraCode);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@site", siteCode);
        cmd.Parameters.AddWithValue("@zone", (object?)zone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@station", (object?)station ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cp", (object?)checkpointCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rtsp", (object?)rtspUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@purpose", purpose);
        cmd.Parameters.AddWithValue("@status", status);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Pins a camera at (x, y) on a site map. Fractions 0..1.</summary>
    public async Task PlaceAsync(int cameraId, int siteMapId, decimal x, decimal y, int? headingDeg, CancellationToken ct = default)
    {
        x = Math.Clamp(x, 0m, 1m); y = Math.Clamp(y, 0m, 1m);
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.CameraPlacement AS t
USING (SELECT @cam AS CameraId, @map AS SiteMapId) AS s
  ON t.CameraId = s.CameraId AND t.SiteMapId = s.SiteMapId
WHEN MATCHED THEN UPDATE SET X=@x, Y=@y, HeadingDeg=@h, UpdatedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CameraId, SiteMapId, X, Y, HeadingDeg) VALUES (@cam, @map, @x, @y, @h);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cam", cameraId);
        cmd.Parameters.AddWithValue("@map", siteMapId);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y);
        cmd.Parameters.AddWithValue("@h", (object?)headingDeg ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task HeartbeatAsync(string cameraCode, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("UPDATE ops.Camera SET LastSeenUtc=SYSUTCDATETIME(), Status='Active' WHERE CameraCode=@c;", conn);
        cmd.Parameters.AddWithValue("@c", cameraCode);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------- site maps ----------

    public sealed record SiteMapRow(int SiteMapId, string SiteCode, string Name, string? ImageUri, int Width, int Height);

    public async Task<IReadOnlyList<SiteMapRow>> ListMapsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT SiteMapId, SiteCode, Name, ImageUri, Width, Height FROM ops.SiteMap ORDER BY SiteCode;", conn);
        var list = new List<SiteMapRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new SiteMapRow(r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3), r.GetInt32(4), r.GetInt32(5)));
        return list;
    }

    public async Task<int> UpsertMapAsync(string siteCode, string name, string? imageUri, int width, int height, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.SiteMap AS t
USING (SELECT @site AS SiteCode) AS s ON t.SiteCode = s.SiteCode
WHEN MATCHED THEN UPDATE SET Name=@name, ImageUri=@img, Width=@w, Height=@h
WHEN NOT MATCHED THEN INSERT (SiteCode, Name, ImageUri, Width, Height) VALUES (@site, @name, @img, @w, @h);
SELECT SiteMapId FROM ops.SiteMap WHERE SiteCode=@site;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@site", siteCode);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@img", (object?)imageUri ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@w", width);
        cmd.Parameters.AddWithValue("@h", height);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
