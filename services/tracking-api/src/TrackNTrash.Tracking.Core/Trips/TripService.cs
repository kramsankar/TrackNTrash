using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core.Services;

namespace TrackNTrash.Tracking.Core.Trips;

/// <summary>
/// Trip creation, loading (with wrong-trip detection) and departure.
/// Loading/departure transition affected shipment lines by emitting scan events through the
/// shared IngestionService, so all state changes flow through the one audited state machine.
/// </summary>
public sealed class TripService
{
    private readonly ITripStore _trips;
    private readonly IngestionService _ingestion;
    private readonly ExceptionSeverityMatrix _severity;
    private readonly ILogger<TripService> _log;

    public TripService(ITripStore trips, IngestionService ingestion, ExceptionSeverityMatrix severity, ILogger<TripService> log)
    {
        _trips = trips;
        _ingestion = ingestion;
        _severity = severity;
        _log = log;
    }

    public async Task<Trip> CreateAsync(CreateTripRequest req, CancellationToken ct = default)
    {
        // The number must be unique across restarts, so it comes from whatever the store
        // uses for identity — a table max for SQL, a counter for the in-memory impl.
        long id = await _trips.NextSequenceAsync(ct);
        string tripNumber = $"TRIP-{id:D6}";
        var trip = new Trip
        {
            TripId = id,
            TripNumber = tripNumber,
            ManifestQr = $"MANIFEST-{tripNumber}",
            VehicleReg = req.VehicleReg,
            DriverName = req.DriverName,
            DriverId = req.DriverId,
            RouteCode = req.RouteCode,
            Status = TripStatus.Planned,
            Stops = req.Stops.OrderBy(s => s.Sequence).ToList(),
            Loads = req.PlannedTrays.Select(pt => new TripLoadState { Planned = pt }).ToList()
        };
        var saved = await _trips.AddAsync(trip, ct);
        _log.LogInformation("Created {Trip} with {Trays} trays, {Stops} stops",
            saved.TripNumber, saved.Loads.Count, saved.Stops.Count);
        return saved;
    }

    public Task<Trip?> GetAsync(string tripNumberOrManifest, CancellationToken ct = default)
        => tripNumberOrManifest.StartsWith("MANIFEST-", StringComparison.OrdinalIgnoreCase)
            ? _trips.GetByManifestQrAsync(tripNumberOrManifest, ct)
            : _trips.GetByNumberAsync(tripNumberOrManifest, ct);

    /// <summary>
    /// Driver scans a tray at the loading dock. Validates the tray belongs to this trip's manifest.
    /// A wrong-trip scan yields an immediate WrongTrip result (with the correct trip number if the
    /// tray is planned elsewhere) and raises a WrongTrip exception — it does NOT load the tray.
    /// </summary>
    public async Task<LoadScanResult> LoadTrayScanAsync(string tripNumber, string trayQr, string deviceId, string? userId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByNumberAsync(tripNumber, ct);
        if (trip is null)
            return new LoadScanResult { Outcome = LoadScanOutcome.WrongTrip, TrayQr = trayQr, TripNumber = tripNumber,
                Message = $"Trip {tripNumber} not found." };

        if (trip.Status is TripStatus.Loaded or TripStatus.Departed or TripStatus.Completed or TripStatus.Cancelled)
            return new LoadScanResult { Outcome = LoadScanOutcome.TripLocked, TrayQr = trayQr, TripNumber = tripNumber,
                Message = $"Trip {tripNumber} is {trip.Status} — loading closed." };

        var load = trip.FindTray(trayQr);
        if (load is null)
        {
            // Wrong trip: find where the tray actually belongs.
            var correctTrip = await _trips.FindTripForTrayAsync(trayQr, ct);
            var ex = new TrackException
            {
                Type = ExceptionType.WrongTrip,
                Severity = _severity.For(ExceptionType.WrongTrip),
                Checkpoint = "VehicleLoad",
                TripId = trip.TripId,
                Detail = $"Tray {trayQr} scanned onto {tripNumber} but "
                         + (correctTrip is not null ? $"belongs to {correctTrip.TripNumber}." : "is not on any planned trip."),
            };
            _log.LogWarning("Wrong-trip scan: {Tray} on {Trip} (correct={Correct})", trayQr, tripNumber, correctTrip?.TripNumber);
            return new LoadScanResult
            {
                Outcome = LoadScanOutcome.WrongTrip,
                TrayQr = trayQr,
                TripNumber = tripNumber,
                CorrectTripNumber = correctTrip?.TripNumber,
                Message = correctTrip is not null
                    ? $"WRONG TRIP — load on {correctTrip.TripNumber}"
                    : "WRONG TRIP — tray not planned on any trip",
                Exception = ex
            };
        }

        if (load.Loaded)
            return new LoadScanResult { Outcome = LoadScanOutcome.AlreadyLoaded, TrayQr = trayQr, TripNumber = tripNumber,
                Message = $"Tray {trayQr} already loaded." };

        // Load it + advance the tray's order lines to Loaded via the state machine.
        load.Loaded = true;
        load.LoadedUtc = DateTimeOffset.UtcNow;
        if (trip.Status == TripStatus.Planned) trip.Status = TripStatus.Loading;

        foreach (var orderLineId in load.Planned.OrderLineIds)
        {
            await _ingestion.IngestAsync(new ScanEventInput
            {
                ClientEventId = $"{trip.TripNumber}:{trayQr}:{orderLineId}:load",
                EventType = "TripLoadScan",
                Checkpoint = "VehicleLoad",
                DeviceId = deviceId,
                UserId = userId,
                TrayQr = trayQr,
                TripId = trip.TripId,
                OrderLineId = orderLineId
            }, ct);
        }

        bool locked = false;
        if (trip.AllLoaded)
        {
            trip.Status = TripStatus.Loaded;
            locked = true;
        }
        await _trips.UpdateAsync(trip, ct);

        return new LoadScanResult
        {
            Outcome = LoadScanOutcome.Loaded,
            TrayQr = trayQr,
            TripNumber = tripNumber,
            TripNowLocked = locked,
            Message = locked ? $"Loaded. Trip {tripNumber} complete and locked." : $"Loaded {trayQr}."
        };
    }

    /// <summary>Telemetry departure (geofence) → transition all loaded lines to InTransit.</summary>
    public async Task<bool> DepartAsync(string tripNumber, string deviceId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByNumberAsync(tripNumber, ct);
        if (trip is null || trip.Status is not (TripStatus.Loaded or TripStatus.Loading)) return false;

        trip.Status = TripStatus.Departed;
        trip.DepartedUtc = DateTimeOffset.UtcNow;

        foreach (var load in trip.Loads.Where(l => l.Loaded))
            foreach (var orderLineId in load.Planned.OrderLineIds)
                await _ingestion.IngestAsync(new ScanEventInput
                {
                    ClientEventId = $"{trip.TripNumber}:{orderLineId}:depart",
                    EventType = "TelemetryDepart",
                    Checkpoint = "VehicleLoad",
                    DeviceId = deviceId,
                    TripId = trip.TripId,
                    OrderLineId = orderLineId
                }, ct);

        await _trips.UpdateAsync(trip, ct);
        _log.LogInformation("Trip {Trip} departed", trip.TripNumber);
        return true;
    }
}
