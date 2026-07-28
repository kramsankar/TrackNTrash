using Microsoft.Data.SqlClient;

namespace TrackNTrash.LabelApi.Services;

/// <summary>
/// Serial provider backed by Azure SQL sequences created in Module 1
/// (ref.SsccSerialReference, ref.CartonSerialReference, ref.TraySequence).
/// </summary>
public sealed class SqlSerialNumberProvider : ISerialNumberProvider
{
    private readonly string _connectionString;

    public SqlSerialNumberProvider(string connectionString)
        => _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));

    public Task<long> NextCartonSerialAsync(CancellationToken ct = default)
        => NextLongAsync("ref.CartonSerialReference", ct);

    public Task<long> NextSsccReferenceAsync(CancellationToken ct = default)
        => NextLongAsync("ref.SsccSerialReference", ct);

    public async Task<int> NextTraySequenceAsync(CancellationToken ct = default)
        => (int)await NextLongAsync("ref.TraySequence", ct);

    private async Task<long> NextLongAsync(string sequenceName, CancellationToken ct)
    {
        // Sequence name is from a fixed internal allow-list, never user input.
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT NEXT VALUE FOR {sequenceName};";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }
}
