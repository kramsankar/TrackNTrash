namespace TrackNTrash.Tracking.Core.Trips;

public enum TripStatus { Planned, Loading, Loaded, Departed, Completed, Cancelled }

/// <summary>A drop on the route.</summary>
public sealed record TripStopDef
{
    public int Sequence { get; init; }
    public string StoreCode { get; init; } = "";
    public int? StoreId { get; init; }
}

/// <summary>A tray planned onto a trip, with the stop it is destined for and its order lines.</summary>
public sealed record PlannedTray
{
    public string TrayQr { get; init; } = "";
    public int StopSequence { get; init; }
    /// <summary>Order lines carried by this tray (used to transition shipment state on load).</summary>
    public IReadOnlyList<long> OrderLineIds { get; init; } = Array.Empty<long>();
}

public sealed record CreateTripRequest
{
    public string VehicleReg { get; init; } = "";
    public string? DriverName { get; init; }
    public string? DriverId { get; init; }
    public string? RouteCode { get; init; }
    public IReadOnlyList<TripStopDef> Stops { get; init; } = Array.Empty<TripStopDef>();
    public IReadOnlyList<PlannedTray> PlannedTrays { get; init; } = Array.Empty<PlannedTray>();
}

/// <summary>Loading status of a single planned tray.</summary>
public sealed class TripLoadState
{
    public required PlannedTray Planned { get; init; }
    public bool Loaded { get; set; }
    public DateTimeOffset? LoadedUtc { get; set; }
    public bool Unloaded { get; set; }
    public DateTimeOffset? UnloadedUtc { get; set; }
}

public sealed class Trip
{
    public long TripId { get; init; }
    public string TripNumber { get; init; } = "";
    public string ManifestQr { get; init; } = "";
    public string VehicleReg { get; init; } = "";
    public string? DriverName { get; init; }
    public string? DriverId { get; init; }
    public string? RouteCode { get; init; }
    public TripStatus Status { get; set; } = TripStatus.Planned;
    public IReadOnlyList<TripStopDef> Stops { get; init; } = Array.Empty<TripStopDef>();
    public List<TripLoadState> Loads { get; init; } = new();
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DepartedUtc { get; set; }

    public bool AllLoaded => Loads.Count > 0 && Loads.All(l => l.Loaded);
    public TripLoadState? FindTray(string trayQr)
        => Loads.FirstOrDefault(l => string.Equals(l.Planned.TrayQr, trayQr, StringComparison.OrdinalIgnoreCase));
}

public enum LoadScanOutcome { Loaded, AlreadyLoaded, WrongTrip, TripLocked }

/// <summary>Result of a driver scanning a tray at the loading dock.</summary>
public sealed record LoadScanResult
{
    public LoadScanOutcome Outcome { get; init; }
    public string TrayQr { get; init; } = "";
    public string TripNumber { get; init; } = "";
    /// <summary>When WrongTrip: the trip this tray actually belongs to (if known).</summary>
    public string? CorrectTripNumber { get; init; }
    public bool TripNowLocked { get; init; }
    public string Message { get; init; } = "";
    public TrackException? Exception { get; init; }
}
