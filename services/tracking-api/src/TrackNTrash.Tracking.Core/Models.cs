namespace TrackNTrash.Tracking.Core;

/// <summary>Shipment-line state machine positions (mirror of ref.ShipmentState).</summary>
public enum ShipmentState
{
    Ordered,
    Picked,
    Staged,
    Loaded,
    InTransit,
    Received,
    // terminal exceptions
    ShortShipped,
    Damaged,
    WrongStore,
    Lost
}

/// <summary>Triggers that drive shipment-line transitions.</summary>
public enum TransitionTrigger
{
    TrayBuildComplete,      // Ordered  -> Picked
    DockVerificationPass,   // Picked   -> Staged
    TripLoadScan,           // Staged   -> Loaded
    TelemetryDepart,        // Loaded   -> InTransit
    ReceivingComplete,      // InTransit-> Received
    // exception-inducing
    ReconcileShort,         // Picked   -> ShortShipped
    DamageFlag,             // Staged   -> Damaged
    WrongTripScan,          // Loaded   -> WrongStore
    ReceiveSlaBreach        // InTransit-> Lost
}

public enum ExceptionSeverity { Low, Medium, High, Critical }

/// <summary>Exception categories raised across the system.</summary>
public enum ExceptionType
{
    CountMismatch,
    UnknownCarton,
    MissingCarton,
    WrongTrip,
    WrongStore,
    IllegalTransition,
    TrayDwellExceeded,
    NoReceiveSla,
    SuspectedLost,
    Damaged,
    ShortShipped
}

/// <summary>Normalized inbound scan/verification event (from mobile, Power Automate, or IoT Hub).</summary>
public sealed record ScanEventInput
{
    public string ClientEventId { get; init; } = "";     // idempotency key (with DeviceId)
    public string EventType { get; init; } = "";         // TrayBuildComplete, DockVerification, ...
    public string? Checkpoint { get; init; }             // PickTrayBuild | DispatchDock | VehicleLoad | StoreReceive
    public string DeviceId { get; init; } = "";
    public string? UserId { get; init; }
    public string? ScannedQr { get; init; }
    public long? OrderLineId { get; init; }
    public string? OrderLineRef { get; init; }           // alternate resolver (e.g. "SO1001-1")
    public long? CartonId { get; init; }
    public int? TrayId { get; init; }
    public string? TrayQr { get; init; }
    public long? TripId { get; init; }
    public int? StoreId { get; init; }
    public string? Verdict { get; init; }                // PASS | COUNT_MISMATCH | UNKNOWN_CARTON | ...
    public string? MetaJson { get; init; }
    public DateTimeOffset EventUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>An event as persisted to the append-only log.</summary>
public sealed record StoredScanEvent
{
    public long ScanEventId { get; init; }
    public required ScanEventInput Input { get; init; }
    public DateTimeOffset IngestedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Current derived state of an order line.</summary>
public sealed class ShipmentLineStateRecord
{
    public long OrderLineId { get; set; }
    public ShipmentState CurrentState { get; set; } = ShipmentState.Ordered;
    public ShipmentState? PreviousState { get; set; }
    public long? LastEventId { get; set; }
    public int PickedCartons { get; set; }
    public int ReceivedCartons { get; set; }
    public DateTimeOffset StateEnteredUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Domain exception record produced by the state machine / rules.</summary>
public sealed record TrackException
{
    public ExceptionType Type { get; init; }
    public ExceptionSeverity Severity { get; init; }
    public string? Checkpoint { get; init; }
    public long? OrderLineId { get; init; }
    public long? CartonId { get; init; }
    public int? TrayId { get; init; }
    public long? TripId { get; init; }
    public int? StoreId { get; init; }
    public long? TriggeringEventId { get; init; }
    public string Detail { get; init; } = "";
    public string? FrameBlobUri { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Expected contents of a tray, synced to the edge for dock verification.</summary>
public sealed record TrayManifest
{
    public required string TrayQr { get; init; }
    public long? TripId { get; init; }
    public int ExpectedCartonCount { get; init; }
    public IReadOnlyList<string> ExpectedCartonPayloads { get; init; } = Array.Empty<string>();
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Outcome of ingesting a single event.</summary>
public sealed record IngestResult
{
    public bool Accepted { get; init; }
    public bool Duplicate { get; init; }
    public long? ScanEventId { get; init; }
    public ShipmentState? NewState { get; init; }
    public bool TransitionLegal { get; init; } = true;
    public IReadOnlyList<TrackException> Exceptions { get; init; } = Array.Empty<TrackException>();
}
