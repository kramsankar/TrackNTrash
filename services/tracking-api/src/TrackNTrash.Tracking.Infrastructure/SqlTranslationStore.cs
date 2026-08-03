using Microsoft.Data.SqlClient;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// Reads and writes ref.Language and ops.Translation (migration 010).
///
/// The rule everything here follows: a missing translation falls back to the base English
/// value, never to blank. A picker who switches to Tamil and finds half the screen empty is
/// worse off than one who sees English — the fallback is the feature, not a safety net.
/// </summary>
public sealed class SqlTranslationStore
{
    private readonly string _cs;
    public SqlTranslationStore(string cs) => _cs = cs;

    public sealed record Language(string Code, string EnglishName, string NativeName, int SortOrder);

    public const string DefaultLanguage = "en";

    public async Task<IReadOnlyList<Language>> LanguagesAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT LanguageCode, EnglishName, NativeName, SortOrder FROM ref.Language " +
            "WHERE IsActive = 1 ORDER BY SortOrder, LanguageCode;", conn);
        var list = new List<Language>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new Language(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>
    /// Validates a requested language against the active list. Anything unknown resolves to
    /// English rather than erroring — a stale preference on an old handset should degrade to
    /// a readable screen, not a failed request.
    /// </summary>
    public async Task<string> ResolveAsync(string? requested, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requested)) return DefaultLanguage;

        // Accept-Language is a weighted list ("ta-IN,ta;q=0.9,en;q=0.8"); take the codes in
        // order and use the first one actually on offer.
        var candidates = requested
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(';')[0].Trim())
            .Where(c => c.Length > 0)
            .SelectMany(c => c.Contains('-') ? new[] { c, c.Split('-')[0] } : new[] { c })
            .ToList();
        if (candidates.Count == 0) return DefaultLanguage;

        var active = (await LanguagesAsync(ct)).Select(l => l.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
            if (active.Contains(c))
                return active.First(a => string.Equals(a, c, StringComparison.OrdinalIgnoreCase));
        return DefaultLanguage;
    }

    /// <summary>All translations for one entity type in one language, keyed "entityKey|field".</summary>
    public async Task<Dictionary<string, string>> BundleAsync(string entityType, string language,
        CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase)) return map;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT EntityKey, FieldName, Value FROM ops.Translation " +
            "WHERE EntityType = @t AND LanguageCode = @l;", conn);
        cmd.Parameters.AddWithValue("@t", entityType);
        cmd.Parameters.AddWithValue("@l", language);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            map[$"{r.GetString(0)}|{r.GetString(1)}"] = r.GetString(2);
        return map;
    }

    /// <summary>Every reference bundle the console needs in one round trip.</summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> ReferenceBundleAsync(
        string language, CancellationToken ct = default)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase)) return result;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT EntityType, EntityKey, Value FROM ops.Translation " +
            "WHERE LanguageCode = @l AND FieldName = 'name' " +
            "AND EntityType IN ('checkpoint','state','exception','severity','role');", conn);
        cmd.Parameters.AddWithValue("@l", language);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var type = r.GetString(0);
            if (!result.TryGetValue(type, out var inner))
                result[type] = inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            inner[r.GetString(1)] = r.GetString(2);
        }
        return result;
    }

    /// <summary>Translations for one record, so an editor can see what exists before changing it.</summary>
    public async Task<IReadOnlyList<(string Language, string Field, string Value)>> ForEntityAsync(
        string entityType, string entityKey, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT LanguageCode, FieldName, Value FROM ops.Translation " +
            "WHERE EntityType = @t AND EntityKey = @k ORDER BY LanguageCode, FieldName;", conn);
        cmd.Parameters.AddWithValue("@t", entityType);
        cmd.Parameters.AddWithValue("@k", entityKey);
        var list = new List<(string, string, string)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        return list;
    }

    /// <summary>
    /// Upserts one translation. An empty value deletes it rather than storing blank, so
    /// clearing a bad translation restores the English fallback instead of blanking the field.
    /// </summary>
    public async Task UpsertAsync(string entityType, string entityKey, string field,
        string language, string? value, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        if (string.IsNullOrWhiteSpace(value))
        {
            await using var del = new SqlCommand(
                "DELETE FROM ops.Translation WHERE EntityType=@t AND EntityKey=@k " +
                "AND FieldName=@f AND LanguageCode=@l;", conn);
            del.Parameters.AddWithValue("@t", entityType);
            del.Parameters.AddWithValue("@k", entityKey);
            del.Parameters.AddWithValue("@f", field);
            del.Parameters.AddWithValue("@l", language);
            await del.ExecuteNonQueryAsync(ct);
            return;
        }

        await using var cmd = new SqlCommand(@"
MERGE ops.Translation WITH (HOLDLOCK) AS t
USING (SELECT @t AS EntityType, @k AS EntityKey, @f AS FieldName, @l AS LanguageCode) AS s
   ON t.EntityType = s.EntityType AND t.EntityKey = s.EntityKey
  AND t.FieldName = s.FieldName AND t.LanguageCode = s.LanguageCode
WHEN MATCHED THEN UPDATE SET Value = @v, UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (EntityType, EntityKey, FieldName, LanguageCode, Value)
                      VALUES (@t, @k, @f, @l, @v);", conn);
        cmd.Parameters.AddWithValue("@t", entityType);
        cmd.Parameters.AddWithValue("@k", entityKey);
        cmd.Parameters.AddWithValue("@f", field);
        cmd.Parameters.AddWithValue("@l", language);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
