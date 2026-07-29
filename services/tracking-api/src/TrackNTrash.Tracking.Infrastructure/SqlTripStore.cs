using Microsoft.Data.SqlClient;
using TrackNTrash.Tracking.Core.Trips;

namespace TrackNTrash.Tracking.Infrastructure;

/// <summary>
/// SQL-backed trip store over ops.Trip / ops.TripStop / ops.TripLoad.
///
/// Replaces the in-memory store that shipped first: trips survived only as long as the
/// process, so an App Service recycle silently discarded every planned and in-flight
/// trip while the API still reported success.
/// </summary>
public sealed class SqlTripStore : ITripStore
{
    private readonly string _cs;
    public SqlTripStore(string cs) => _cs = cs;

    public async Task<Trip> AddAsync(Trip trip, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            // Vehicle is a master; create on demand so trip planning never fails on it.
            const string head = @"
DECLARE @vid INT = (SELECT VehicleId FROM ops.Vehicle WHERE Registration=@reg);
IF @vid IS NULL
BEGIN
    INSERT INTO ops.Vehicle (Registration) VALUES (@reg);
    SET @vid = SCOPE_IDENTITY();
END
INSERT INTO ops.Trip (TripNumber, VehicleId, DriverName, DriverId, RouteCode, ManifestQr, TripStatus)
VALUES (@num, @vid, @dname, @did, @route, @mqr, @status);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            long tripId;
            await using (var cmd = new SqlCommand(head, conn, tx))
            {
                cmd.Parameters.AddWithValue("@reg", trip.VehicleReg);
                cmd.Parameters.AddWithValue("@num", trip.TripNumber);
                cmd.Parameters.AddWithValue("@dname", (object?)trip.DriverName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@did", (object?)trip.DriverId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@route", (object?)trip.RouteCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mqr", trip.ManifestQr);
                cmd.Parameters.AddWithValue("@status", trip.Status.ToString());
                tripId = (long)(await cmd.ExecuteScalarAsync(ct))!;
            }

            var stopIds = new Dictionary<int, long>();
            foreach (var stop in trip.Stops)
            {
                const string sql = @"
DECLARE @sid INT = (SELECT StoreId FROM ops.Store WHERE StoreCode=@code);
IF @sid IS NULL
BEGIN
    INSERT INTO ops.Store (StoreCode, Name) VALUES (@code, @code);
    SET @sid = SCOPE_IDENTITY();
END
INSERT INTO ops.TripStop (TripId, StoreId, StopSequence) VALUES (@trip, @sid, @seq);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@code", stop.StoreCode);
                cmd.Parameters.AddWithValue("@trip", tripId);
                cmd.Parameters.AddWithValue("@seq", stop.Sequence);
                stopIds[stop.Sequence] = (long)(await cmd.ExecuteScalarAsync(ct))!;
            }

            foreach (var load in trip.Loads)
            {
                const string sql = @"
DECLARE @tid INT = (SELECT TrayId FROM ops.Tray WHERE TrayQr=@qr);
IF @tid IS NULL
BEGIN
    INSERT INTO ops.Tray (TrayQr, SiteCode) VALUES (@qr, 'UNKNOWN');
    SET @tid = SCOPE_IDENTITY();
END
INSERT INTO ops.TripLoad (TripId, TrayId, TripStopId, IsPlanned, OrderLineIds)
VALUES (@trip, @tid, @stop, 1, @lines);";
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@qr", load.Planned.TrayQr);
                cmd.Parameters.AddWithValue("@trip", tripId);
                cmd.Parameters.AddWithValue("@stop",
                    stopIds.TryGetValue(load.Planned.StopSequence, out var s) ? s : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lines", string.Join(",", load.Planned.OrderLineIds));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Rehydrate(trip, tripId);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Next trip number, derived from the table so it survives a restart.</summary>
    public async Task<long> NextSequenceAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT ISNULL(MAX(TripId),0)+1 FROM ops.Trip;", conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public Task<Trip?> GetByNumberAsync(string tripNumber, CancellationToken ct = default)
        => LoadAsync("t.TripNumber = @k", tripNumber, ct);

    public Task<Trip?> GetByManifestQrAsync(string manifestQr, CancellationToken ct = default)
        => LoadAsync("t.ManifestQr = @k", manifestQr, ct);

    public async Task<Trip?> FindTripForTrayAsync(string trayQr, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        const string sql = @"
SELECT TOP 1 t.TripNumber FROM ops.Trip t
JOIN ops.TripLoad l ON l.TripId = t.TripId
JOIN ops.Tray y ON y.TrayId = l.TrayId
WHERE y.TrayQr = @qr AND t.TripStatus NOT IN ('Completed','Cancelled')
ORDER BY t.TripId DESC;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@qr", trayQr);
        var number = await cmd.ExecuteScalarAsync(ct) as string;
        return number is null ? null : await GetByNumberAsync(number, ct);
    }

    public async Task UpdateAsync(Trip trip, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using (var cmd = new SqlCommand(
            "UPDATE ops.Trip SET TripStatus=@s, ActualDepartureUtc=@dep WHERE TripId=@id;", conn))
        {
            cmd.Parameters.AddWithValue("@s", trip.Status.ToString());
            cmd.Parameters.AddWithValue("@dep", (object?)trip.DepartedUtc?.UtcDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", trip.TripId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach (var load in trip.Loads)
        {
            await using var cmd = new SqlCommand(@"
UPDATE l SET LoadedUtc=@loaded, UnloadedUtc=@unloaded
FROM ops.TripLoad l JOIN ops.Tray y ON y.TrayId=l.TrayId
WHERE l.TripId=@trip AND y.TrayQr=@qr;", conn);
            cmd.Parameters.AddWithValue("@loaded", (object?)load.LoadedUtc?.UtcDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@unloaded", (object?)load.UnloadedUtc?.UtcDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@trip", trip.TripId);
            cmd.Parameters.AddWithValue("@qr", load.Planned.TrayQr);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Trip is a class, so the persisted id is applied by rebuilding it.</summary>
    private static Trip Rehydrate(Trip t, long tripId)
    {
        var copy = new Trip
        {
            TripId = tripId, TripNumber = t.TripNumber, ManifestQr = t.ManifestQr,
            VehicleReg = t.VehicleReg, DriverName = t.DriverName, DriverId = t.DriverId,
            RouteCode = t.RouteCode, Status = t.Status, CreatedUtc = t.CreatedUtc,
            DepartedUtc = t.DepartedUtc, Stops = t.Stops,
        };
        copy.Loads.AddRange(t.Loads);
        return copy;
    }

    private async Task<Trip?> LoadAsync(string where, string key, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        long tripId;
        Trip trip;
        await using (var cmd = new SqlCommand($@"
SELECT t.TripId, t.TripNumber, t.ManifestQr, v.Registration, t.DriverName, t.DriverId,
       t.RouteCode, t.TripStatus, t.CreatedUtc, t.ActualDepartureUtc
FROM ops.Trip t JOIN ops.Vehicle v ON v.VehicleId = t.VehicleId WHERE {where};", conn))
        {
            cmd.Parameters.AddWithValue("@k", key);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            tripId = r.GetInt64(0);
            trip = new Trip
            {
                TripId = tripId,
                TripNumber = r.GetString(1),
                ManifestQr = r.GetString(2),
                VehicleReg = r.GetString(3),
                DriverName = r.IsDBNull(4) ? null : r.GetString(4),
                DriverId = r.IsDBNull(5) ? null : r.GetString(5),
                RouteCode = r.IsDBNull(6) ? null : r.GetString(6),
                Status = Enum.Parse<TripStatus>(r.GetString(7)),
                CreatedUtc = new DateTimeOffset(r.GetDateTime(8), TimeSpan.Zero),
                DepartedUtc = r.IsDBNull(9) ? null : new DateTimeOffset(r.GetDateTime(9), TimeSpan.Zero),
            };
        }

        var stops = new List<TripStopDef>();
        await using (var cmd = new SqlCommand(@"
SELECT s.StopSequence, st.StoreCode, s.StoreId FROM ops.TripStop s
JOIN ops.Store st ON st.StoreId = s.StoreId WHERE s.TripId=@id ORDER BY s.StopSequence;", conn))
        {
            cmd.Parameters.AddWithValue("@id", tripId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                stops.Add(new TripStopDef { Sequence = r.GetInt32(0), StoreCode = r.GetString(1), StoreId = r.GetInt32(2) });
        }

        var loads = new List<TripLoadState>();
        await using (var cmd = new SqlCommand(@"
SELECT y.TrayQr, ISNULL(s.StopSequence,1), l.OrderLineIds, l.LoadedUtc, l.UnloadedUtc
FROM ops.TripLoad l JOIN ops.Tray y ON y.TrayId=l.TrayId
LEFT JOIN ops.TripStop s ON s.TripStopId=l.TripStopId
WHERE l.TripId=@id;", conn))
        {
            cmd.Parameters.AddWithValue("@id", tripId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var lineCsv = r.IsDBNull(2) ? "" : r.GetString(2);
                loads.Add(new TripLoadState
                {
                    Planned = new PlannedTray
                    {
                        TrayQr = r.GetString(0),
                        StopSequence = r.GetInt32(1),
                        OrderLineIds = lineCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(long.Parse).ToList(),
                    },
                    Loaded = !r.IsDBNull(3),
                    LoadedUtc = r.IsDBNull(3) ? null : new DateTimeOffset(r.GetDateTime(3), TimeSpan.Zero),
                    Unloaded = !r.IsDBNull(4),
                    UnloadedUtc = r.IsDBNull(4) ? null : new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero),
                });
            }
        }

        var loaded = new Trip
        {
            TripId = trip.TripId, TripNumber = trip.TripNumber, ManifestQr = trip.ManifestQr,
            VehicleReg = trip.VehicleReg, DriverName = trip.DriverName, DriverId = trip.DriverId,
            RouteCode = trip.RouteCode, Status = trip.Status, CreatedUtc = trip.CreatedUtc,
            DepartedUtc = trip.DepartedUtc, Stops = stops,
        };
        loaded.Loads.AddRange(loads);
        return loaded;
    }
}
