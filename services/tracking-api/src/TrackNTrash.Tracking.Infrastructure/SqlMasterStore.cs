using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// One config-driven CRUD store for every simple master table, so adding a master is a
/// row in <see cref="Masters"/> rather than a new controller. Only tables and columns
/// named in that registry are ever touched — user input never reaches SQL as an
/// identifier, and values always go through parameters.
/// </summary>
public sealed class SqlMasterStore
{
    private readonly string _cs;
    public SqlMasterStore(string cs) => _cs = cs;

    public sealed record MasterDef(
        string Key,          // route segment, e.g. "product"
        string Table,        // ops.Product
        string KeyColumn,    // ProductId
        string[] Columns,    // writable columns
        string OrderBy,
        string Label);       // human name for messages

    /// <summary>The masters this endpoint family serves. Identifiers come only from here.</summary>
    public static readonly IReadOnlyDictionary<string, MasterDef> Masters =
        new Dictionary<string, MasterDef>(StringComparer.OrdinalIgnoreCase)
        {
            ["product"] = new("product", "ops.Product", "ProductId",
                new[] { "Gtin", "Sku", "Name", "Category", "Brand", "UnitsPerCarton", "ItemIdentification", "Uom", "IsActive" },
                "Name", "Product"),
            ["store"] = new("store", "ops.Store", "StoreId",
                new[] { "StoreCode", "Name", "AddressLine", "City", "Region", "PostCode", "Country", "IsActive" },
                "StoreCode", "Store"),
            ["zone"] = new("zone", "ops.Zone", "ZoneId",
                new[] { "SiteCode", "ZoneCode", "Name", "ZoneType", "IsActive" },
                "SiteCode, ZoneCode", "Zone"),
            ["rack"] = new("rack", "ops.Rack", "RackId",
                new[] { "RackCode", "ZoneId", "SiteCode", "Aisle", "Level", "Capacity", "IsActive" },
                "SiteCode, RackCode", "Rack"),
            ["vehicle"] = new("vehicle", "ops.Vehicle", "VehicleId",
                new[] { "Registration", "Description", "TrayCapacity", "IsActive" },
                "Registration", "Vehicle"),
            ["device"] = new("device", "ops.Device", "DeviceId",
                new[] { "DeviceCode", "DeviceType", "SiteCode", "IsActive" },
                "DeviceCode", "Device"),
            ["role"] = new("role", "ops.Role", "RoleId",
                new[] { "RoleName", "Description", "IsAdmin", "IsActive" },
                "RoleName", "Role"),
        };

    private static MasterDef Def(string key) =>
        Masters.TryGetValue(key, out var d) ? d : throw new ArgumentException($"Unknown master '{key}'.");

    /// <summary>Quotes an identifier that has already been validated against the registry.</summary>
    private static string Q(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ListAsync(string key, CancellationToken ct = default)
    {
        var def = Def(key);
        var cols = string.Join(", ", new[] { def.KeyColumn }.Concat(def.Columns).Select(Q));
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"SELECT {cols} FROM {def.Table} ORDER BY {def.OrderBy};", conn);
        var rows = new List<Dictionary<string, object?>>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < r.FieldCount; i++)
                row[Camel(r.GetName(i))] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<int> CreateAsync(string key, JsonElement body, CancellationToken ct = default)
    {
        var def = Def(key);
        var supplied = def.Columns.Where(c => Has(body, c)).ToArray();
        if (supplied.Length == 0) throw new ArgumentException("No known fields supplied.");

        var colList = string.Join(", ", supplied.Select(Q));
        var parList = string.Join(", ", supplied.Select((c, i) => "@p" + i));
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            $"INSERT INTO {def.Table} ({colList}) VALUES ({parList}); SELECT CAST(SCOPE_IDENTITY() AS int);", conn);
        for (int i = 0; i < supplied.Length; i++)
            cmd.Parameters.AddWithValue("@p" + i, Value(body, supplied[i]));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<bool> UpdateAsync(string key, int id, JsonElement body, CancellationToken ct = default)
    {
        var def = Def(key);
        var supplied = def.Columns.Where(c => Has(body, c)).ToArray();
        if (supplied.Length == 0) return false;

        var sets = string.Join(", ", supplied.Select((c, i) => $"{Q(c)} = @p{i}"));
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"UPDATE {def.Table} SET {sets} WHERE {Q(def.KeyColumn)} = @id;", conn);
        for (int i = 0; i < supplied.Length; i++)
            cmd.Parameters.AddWithValue("@p" + i, Value(body, supplied[i]));
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>
    /// Soft-deletes when the master has an IsActive flag (master data is referenced by
    /// history, so hard deletes would orphan it); hard-deletes only where it does not.
    /// </summary>
    public async Task<bool> DeleteAsync(string key, int id, CancellationToken ct = default)
    {
        var def = Def(key);
        bool soft = def.Columns.Contains("IsActive", StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        var sql = soft
            ? $"UPDATE {def.Table} SET [IsActive] = 0 WHERE {Q(def.KeyColumn)} = @id;"
            : $"DELETE FROM {def.Table} WHERE {Q(def.KeyColumn)} = @id;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ---- JSON helpers: the console sends camelCase, the columns are PascalCase ----

    private static bool Has(JsonElement body, string column) =>
        body.ValueKind == JsonValueKind.Object &&
        (body.TryGetProperty(Camel(column), out _) || body.TryGetProperty(column, out _));

    private static object Value(JsonElement body, string column)
    {
        if (!body.TryGetProperty(Camel(column), out var el) && !body.TryGetProperty(column, out el))
            return DBNull.Value;
        return el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => DBNull.Value,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.TryGetInt32(out var i) ? i : el.GetDecimal(),
            JsonValueKind.String => string.IsNullOrEmpty(el.GetString()) ? DBNull.Value : el.GetString()!,
            _ => el.ToString(),
        };
    }

    private static string Camel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
