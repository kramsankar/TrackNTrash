using TrackNTrash.Tracking.Core.Trips;

namespace TrackNTrash.Tracking.Api;

public sealed record CreateTripDto
{
    public string VehicleReg { get; init; } = "";
    public string? DriverName { get; init; }
    public string? DriverId { get; init; }
    public string? RouteCode { get; init; }
    public List<StopDto> Stops { get; init; } = new();
    public List<PlannedTrayDto> PlannedTrays { get; init; } = new();

    public CreateTripRequest ToRequest() => new()
    {
        VehicleReg = VehicleReg,
        DriverName = DriverName,
        DriverId = DriverId,
        RouteCode = RouteCode,
        Stops = Stops.Select(s => new TripStopDef { Sequence = s.Sequence, StoreCode = s.StoreCode, StoreId = s.StoreId }).ToList(),
        PlannedTrays = PlannedTrays.Select(p => new PlannedTray
        {
            TrayQr = p.TrayQr, StopSequence = p.StopSequence, OrderLineIds = p.OrderLineIds
        }).ToList()
    };
}

public sealed record StopDto
{
    public int Sequence { get; init; }
    public string StoreCode { get; init; } = "";
    public int? StoreId { get; init; }
}

public sealed record PlannedTrayDto
{
    public string TrayQr { get; init; } = "";
    public int StopSequence { get; init; }
    public List<long> OrderLineIds { get; init; } = new();
}

public sealed record LoadScanDto
{
    public string TrayQr { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public string? UserId { get; init; }
}

/// <summary>Geofence / telematics webhook payload (POST /events/telemetry).</summary>
public sealed record TelemetryDto
{
    public string TripNumber { get; init; } = "";
    public string Event { get; init; } = "";   // "depart" | "arrive"
    public int? StopSequence { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public string? DeviceId { get; init; }
    public DateTimeOffset? OccurredUtc { get; init; }
}
