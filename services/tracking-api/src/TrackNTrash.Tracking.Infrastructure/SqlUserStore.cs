using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Local application users for username/password sign-in (shared warehouse devices).
/// Passwords use PBKDF2-HMAC-SHA256 with a per-user salt, stored as base64(salt):base64(hash).
/// Entra ID users are not stored here — they authenticate against Azure AD.
/// </summary>
public sealed class SqlUserStore
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly string _cs;
    public SqlUserStore(string cs) => _cs = cs;

    public sealed record AppUser(int UserId, string Username, string DisplayName, string[] Roles);

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);   // constant-time
        }
        catch { return false; }
    }

    /// <summary>Validates credentials; returns the user when correct, else null.</summary>
    public async Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"SELECT UserId, Username, DisplayName, PasswordHash, Roles
                             FROM ops.AppUser WHERE Username = @u AND IsActive = 1;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);

        int id; string user, display, hash, roles;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;
            id = r.GetInt32(0); user = r.GetString(1); display = r.GetString(2);
            hash = r.GetString(3); roles = r.GetString(4);
        }
        if (!VerifyPassword(password, hash)) return null;

        await using (var touch = new SqlCommand("UPDATE ops.AppUser SET LastLoginUtc = SYSUTCDATETIME() WHERE UserId = @id;", conn))
        {
            touch.Parameters.AddWithValue("@id", id);
            await touch.ExecuteNonQueryAsync(ct);
        }
        return new AppUser(id, user, display, roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Creates or updates a user (idempotent by username).</summary>
    public async Task UpsertAsync(string username, string displayName, string password, string roles, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
MERGE ops.AppUser AS t
USING (SELECT @u AS Username) AS s ON t.Username = s.Username
WHEN MATCHED THEN UPDATE SET DisplayName=@d, PasswordHash=@p, Roles=@r, IsActive=1
WHEN NOT MATCHED THEN INSERT (Username, DisplayName, PasswordHash, Roles) VALUES (@u, @d, @p, @r);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@d", displayName);
        cmd.Parameters.AddWithValue("@p", HashPassword(password));
        cmd.Parameters.AddWithValue("@r", roles);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>True when at least one user exists (used to decide first-run seeding).</summary>
    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM ops.AppUser;", conn);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT UserId, Username, DisplayName, Roles FROM ops.AppUser WHERE IsActive=1 ORDER BY Username;", conn);
        var list = new List<AppUser>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new AppUser(r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.GetString(3).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        return list;
    }
}
