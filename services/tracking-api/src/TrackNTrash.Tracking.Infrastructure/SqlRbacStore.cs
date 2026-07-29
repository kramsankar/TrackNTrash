using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Role-based access control, mirroring the BMS model: a role is granted
/// view/create/edit/delete against each form (screen). A user holds one role.
/// Admin roles short-circuit to full access rather than needing every row.
/// </summary>
public sealed class SqlRbacStore
{
    private readonly string _cs;
    public SqlRbacStore(string cs) => _cs = cs;

    public sealed record FormRow(string FormId, string FormName, string FormGroup, int SortOrder);
    public sealed record MappingRow(int RoleId, string RoleName, string FormId, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete);
    public sealed record UserRow(int UserId, string Username, string DisplayName, string? Email,
        int? RoleId, string? RoleName, string? SiteCode, bool IsActive, DateTimeOffset? LastLoginUtc);

    public async Task<IReadOnlyList<FormRow>> ListFormsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT FormId, FormName, FormGroup, SortOrder FROM ops.AppForm ORDER BY SortOrder;", conn);
        var list = new List<FormRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new FormRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
        return list;
    }

    public async Task<IReadOnlyList<MappingRow>> ListMappingsAsync(int? roleId = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        var sql = @"SELECT m.RoleId, r.RoleName, m.FormId, m.CanView, m.CanCreate, m.CanEdit, m.CanDelete
                    FROM ops.RoleFormMapping m JOIN ops.Role r ON r.RoleId = m.RoleId"
                  + (roleId.HasValue ? " WHERE m.RoleId = @rid" : "") + " ORDER BY r.RoleName, m.FormId;";
        await using var cmd = new SqlCommand(sql, conn);
        if (roleId.HasValue) cmd.Parameters.AddWithValue("@rid", roleId.Value);
        var list = new List<MappingRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new MappingRow(r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.GetBoolean(3), r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(6)));
        return list;
    }

    /// <summary>Replaces the permission set for one role/form pair.</summary>
    public async Task SaveMappingAsync(int roleId, string formId, bool view, bool create, bool edit, bool del, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.RoleFormMapping AS t
USING (SELECT @rid AS RoleId, @fid AS FormId) AS s ON t.RoleId=s.RoleId AND t.FormId=s.FormId
WHEN MATCHED THEN UPDATE SET CanView=@v, CanCreate=@c, CanEdit=@e, CanDelete=@d, UpdatedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (RoleId, FormId, CanView, CanCreate, CanEdit, CanDelete)
    VALUES (@rid, @fid, @v, @c, @e, @d);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@rid", roleId);
        cmd.Parameters.AddWithValue("@fid", formId);
        cmd.Parameters.AddWithValue("@v", view);
        cmd.Parameters.AddWithValue("@c", create);
        cmd.Parameters.AddWithValue("@e", edit);
        cmd.Parameters.AddWithValue("@d", del);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<UserRow>> ListUsersAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT u.UserId, u.Username, u.DisplayName, u.Email, u.RoleId, r.RoleName, u.SiteCode, u.IsActive, u.LastLoginUtc
FROM ops.AppUser u LEFT JOIN ops.Role r ON r.RoleId = u.RoleId ORDER BY u.Username;";
        await using var cmd = new SqlCommand(sql, conn);
        var list = new List<UserRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new UserRow(r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6),
                r.GetBoolean(7), r.IsDBNull(8) ? null : new DateTimeOffset(r.GetDateTime(8), TimeSpan.Zero)));
        return list;
    }

    /// <summary>Creates or updates a user. A blank password leaves the existing one intact.</summary>
    public async Task<int> SaveUserAsync(int? userId, string username, string displayName, string? email,
        int? roleId, string? siteCode, string? password, bool isActive, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Keep the legacy comma-separated Roles column in step with the role reference,
        // because issued JWTs still carry role names from it.
        string roleNames = "Dispatcher";
        if (roleId.HasValue)
        {
            await using var rn = new SqlCommand("SELECT RoleName, IsAdmin FROM ops.Role WHERE RoleId=@r;", conn);
            rn.Parameters.AddWithValue("@r", roleId.Value);
            await using var rr = await rn.ExecuteReaderAsync(ct);
            if (await rr.ReadAsync(ct))
                roleNames = rr.GetBoolean(1) ? "Admin,WarehouseManager,Dispatcher" : rr.GetString(0);
        }

        const string sql = @"
DECLARE @id INT = @uid;
IF @id IS NULL SET @id = (SELECT UserId FROM ops.AppUser WHERE Username=@u);
IF @id IS NULL
BEGIN
    INSERT INTO ops.AppUser (Username, DisplayName, PasswordHash, Roles, Email, RoleId, SiteCode, IsActive)
    VALUES (@u, @d, @p, @roles, @email, @rid, @site, @active);
    SET @id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    UPDATE ops.AppUser
       SET DisplayName=@d, Roles=@roles, Email=@email, RoleId=@rid, SiteCode=@site, IsActive=@active,
           PasswordHash = CASE WHEN @p IS NULL THEN PasswordHash ELSE @p END
     WHERE UserId=@id;
END
SELECT @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@d", displayName);
        cmd.Parameters.AddWithValue("@p", string.IsNullOrWhiteSpace(password)
            ? DBNull.Value : SqlUserStore.HashPassword(password));
        cmd.Parameters.AddWithValue("@roles", roleNames);
        cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rid", (object?)roleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@site", (object?)siteCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", isActive);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>Effective permissions for a user, for the console to drive its menu.</summary>
    public async Task<IReadOnlyList<MappingRow>> PermissionsForUserAsync(string username, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
DECLARE @rid INT, @admin BIT = 0;
SELECT @rid = u.RoleId FROM ops.AppUser u WHERE u.Username = @u;
SELECT @admin = ISNULL(IsAdmin,0) FROM ops.Role WHERE RoleId = @rid;
IF @admin = 1
    -- Admin sees every form with full rights, without needing explicit rows.
    SELECT ISNULL(@rid,0) AS RoleId, 'Admin' AS RoleName, f.FormId,
           CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)
    FROM ops.AppForm f;
ELSE
    SELECT m.RoleId, r.RoleName, m.FormId, m.CanView, m.CanCreate, m.CanEdit, m.CanDelete
    FROM ops.RoleFormMapping m JOIN ops.Role r ON r.RoleId = m.RoleId
    WHERE m.RoleId = @rid;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);
        var list = new List<MappingRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new MappingRow(r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.GetBoolean(3), r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(6)));
        return list;
    }
}
