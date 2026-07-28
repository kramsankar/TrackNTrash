# TrackNTrash — Entity-Relationship Diagram (Module 1)

```mermaid
erDiagram
    Store            ||--o{ SalesOrder      : "ships to"
    SalesOrder       ||--o{ OrderLine       : contains
    OrderLine        ||--o{ Carton          : "serialized as"
    OrderLine        ||--|| ShipmentLineState : "current state"
    OrderLine        ||--o{ ShipmentLineStateHistory : "audit"

    Tray             ||--o{ TrayContent     : holds
    Carton           ||--o{ TrayContent     : "bound in"
    Tray             ||--o{ TrayCustody     : "custody chain"
    Tray             ||--o{ TripLoad        : "loaded via"

    Vehicle          ||--o{ Trip            : runs
    Trip             ||--o{ TripStop        : "drops at"
    Store            ||--o{ TripStop        : "receives at"
    Trip             ||--o{ TripLoad        : carries
    TripStop         ||--o{ TripLoad        : "for stop"

    Checkpoint       ||--o{ ScanEvent       : "captured at"
    Checkpoint       ||--o{ EventType       : "typed by"
    Device           ||--o{ ScanEvent       : "captured by"
    EventType        ||--o{ ScanEvent       : classifies
    ScanEvent        ||--o{ ShipmentLineState : "drives (LastEventId)"
    ScanEvent        ||--o{ Exception       : "may trigger"

    Store ||--o{ Exception : "flagged for"
    Tray  ||--o{ Exception : "flagged for"
    Trip  ||--o{ Exception : "flagged for"

    Store {
        int StoreId PK
        nvarchar StoreCode UK
        nvarchar Name
    }
    SalesOrder {
        bigint SalesOrderId PK
        nvarchar OrderNumber UK
        int StoreId FK
        nvarchar ErpReference
    }
    OrderLine {
        bigint OrderLineId PK
        bigint SalesOrderId FK
        int LineNumber
        char Gtin "GTIN-14"
        int ExpectedCartonCount
    }
    Carton {
        bigint CartonId PK
        bigint OrderLineId FK
        char Gtin "GTIN-14"
        nvarchar Serial "(21) <=20"
        char Sscc "SSCC-18 null"
        nvarchar QrPayload
        varchar Status
        int CurrentTrayId FK
    }
    Tray {
        int TrayId PK
        nvarchar TrayQr UK "TRAY-site-seq"
        varchar TrayStatus
        varchar CurrentCustodianType
        nvarchar CurrentCustodianRef
    }
    TrayContent {
        bigint TrayContentId PK
        int TrayId FK
        bigint CartonId FK
        datetime2 BoundUtc
        datetime2 UnboundUtc "null=active"
    }
    Trip {
        bigint TripId PK
        nvarchar TripNumber UK
        int VehicleId FK
        nvarchar ManifestQr
        varchar TripStatus
    }
    TripStop {
        bigint TripStopId PK
        bigint TripId FK
        int StoreId FK
        int StopSequence
    }
    TripLoad {
        bigint TripLoadId PK
        bigint TripId FK
        int TrayId FK
        bigint TripStopId FK
        datetime2 LoadedUtc
        datetime2 UnloadedUtc
    }
    Vehicle {
        int VehicleId PK
        nvarchar Registration UK
    }
    Device {
        int DeviceId PK
        nvarchar DeviceCode UK
        varchar DeviceType
        tinyint CheckpointId FK
    }
    ScanEvent {
        bigint ScanEventId PK
        varchar EventType FK
        tinyint CheckpointId FK
        int DeviceId FK
        nvarchar ClientEventId "idempotency"
        nvarchar ScannedQr
        bigint OrderLineId FK
        bigint CartonId FK
        int TrayId FK
        bigint TripId FK
        varchar Verdict
        datetime2 EventUtc
        rowversion RowVer
    }
    ShipmentLineState {
        bigint OrderLineId PK
        varchar CurrentState FK
        varchar PreviousState FK
        bigint LastEventId FK "audit"
        int PickedCartons
        int ReceivedCartons
    }
    ShipmentLineStateHistory {
        bigint HistoryId PK
        bigint OrderLineId FK
        varchar FromState
        varchar ToState
        bit WasLegal
    }
    TrayCustody {
        bigint TrayCustodyId PK
        int TrayId FK
        varchar FromCustodianType
        varchar ToCustodianType
        bigint TripId FK
        datetime2 CustodyUtc
    }
    Exception {
        bigint ExceptionId PK
        varchar ExceptionType
        varchar Severity
        varchar Status
        tinyint CheckpointId FK
        bigint TriggeringEventId FK
    }
    Checkpoint {
        tinyint CheckpointId PK
        varchar CheckpointCode UK
    }
    EventType {
        varchar EventTypeCode PK
        tinyint CheckpointId FK
    }
    ShipmentState {
        varchar StateCode PK
        bit IsTerminal
        bit IsException
    }
```

## Key modeling notes

- **`ScanEvent` is append-only** — enforced by `ops.TR_ScanEvent_NoMutate` (INSTEAD OF UPDATE/DELETE → THROW). It is the immutable source of truth; everything else is master data or a derived projection.
- **`ShipmentLineState`** is a 1:1 projection per `OrderLine`, carrying `LastEventId` so any state can be traced back to the event that set it. `ShipmentLineStateHistory` keeps the full transition trail, including illegal transitions (`WasLegal = 0`).
- **Custody chain** — `TrayCustody` appends one row per movement (from/to custodian type+ref, optional trip and event refs). Current custodian is the latest row (`ops.vTrayCurrentCustody`) and is also denormalized onto `Tray` for fast reads.
- **Active-binding uniqueness** — filtered unique indexes guarantee a carton is bound to ≤1 tray at a time (`UX_TrayContent_ActiveCarton`) and a tray is loaded on ≤1 trip at a time (`UX_TripLoad_ActiveTray`).
- **GS1 identity** — `Gtin CHAR(14)` (digits-only check), `Serial NVARCHAR(20)` (alphanumeric, ≤20), `Sscc CHAR(18)` (18 digits, filtered-unique when present).
- **Idempotency** — `UQ_ScanEvent_Idem (DeviceId, ClientEventId)` lets offline replay / retries land exactly once.
