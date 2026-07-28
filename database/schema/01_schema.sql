/* =====================================================================================
   TrackNTrash — Dispatch Track-and-Trace  |  Module 1: Operational Schema (Azure SQL)
   -------------------------------------------------------------------------------------
   Design principles
   -----------------
   * ScanEvent is APPEND-ONLY (source of truth). UPDATE/DELETE blocked by trigger.
   * ShipmentLineState is a DERIVED projection of events; carries LastEventId for audit.
   * Every tray movement writes a TrayCustody record (from/to custodian + trip ref).
   * GS1 identity is stored explicitly: GTIN(14), Serial(<=20 alnum), SSCC(18 digits).
   * Idempotency: (DeviceId, ClientEventId) is unique on ScanEvent.
   * Hot-path indexes: lookup by QR, events by order line, open exceptions by checkpoint.

   Schema separation
   -----------------
   * ops   — operational tables (this file)
   * ref   — reference / lookup data
   * mart  — analytics star schema (Module 11, separate file)

   Target: Azure SQL Database (single DB). Run 01_schema.sql then 02_seed_reference.sql.
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required for filtered indexes and indexed views
GO

IF SCHEMA_ID(N'ops') IS NULL EXEC(N'CREATE SCHEMA ops AUTHORIZATION dbo;');
GO
IF SCHEMA_ID(N'ref') IS NULL EXEC(N'CREATE SCHEMA ref AUTHORIZATION dbo;');
GO

/* =====================================================================================
   SEQUENCES  — used by the Label API (Module 2) for SSCC serial reference & tray seq.
   ===================================================================================== */

IF OBJECT_ID(N'ref.SsccSerialReference', N'SO') IS NULL
    CREATE SEQUENCE ref.SsccSerialReference AS BIGINT START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;
GO
IF OBJECT_ID(N'ref.CartonSerialReference', N'SO') IS NULL
    CREATE SEQUENCE ref.CartonSerialReference AS BIGINT START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;
GO
IF OBJECT_ID(N'ref.TraySequence', N'SO') IS NULL
    CREATE SEQUENCE ref.TraySequence AS INT START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;
GO

/* =====================================================================================
   REFERENCE / LOOKUP
   ===================================================================================== */

-- Fixed set of physical checkpoints in the flow.
CREATE TABLE ref.[Checkpoint]
(
    CheckpointId   TINYINT       NOT NULL CONSTRAINT PK_Checkpoint PRIMARY KEY,
    CheckpointCode VARCHAR(20)   NOT NULL,   -- PickTrayBuild | DispatchDock | VehicleLoad | StoreReceive
    Name           NVARCHAR(60)  NOT NULL,
    SortOrder      TINYINT       NOT NULL,
    CONSTRAINT UQ_Checkpoint_Code UNIQUE (CheckpointCode)
);
GO

-- Canonical shipment-line states (state machine positions + terminal exceptions).
CREATE TABLE ref.ShipmentState
(
    StateCode   VARCHAR(20)  NOT NULL CONSTRAINT PK_ShipmentState PRIMARY KEY,  -- Ordered..Received + terminals
    Name        NVARCHAR(60) NOT NULL,
    IsTerminal  BIT          NOT NULL CONSTRAINT DF_ShipmentState_Term DEFAULT (0),
    IsException BIT          NOT NULL CONSTRAINT DF_ShipmentState_Exc  DEFAULT (0),
    SortOrder   TINYINT      NOT NULL
);
GO

-- Event type catalogue (referential integrity for ScanEvent.EventType).
CREATE TABLE ref.EventType
(
    EventTypeCode VARCHAR(30)  NOT NULL CONSTRAINT PK_EventType PRIMARY KEY,
    Name          NVARCHAR(80) NOT NULL,
    CheckpointId  TINYINT      NULL CONSTRAINT FK_EventType_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId)
);
GO

/* =====================================================================================
   MASTER DATA
   ===================================================================================== */

CREATE TABLE ops.Store
(
    StoreId     INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Store PRIMARY KEY,
    StoreCode   NVARCHAR(20)  NOT NULL,
    Name        NVARCHAR(120) NOT NULL,
    AddressLine NVARCHAR(200) NULL,
    City        NVARCHAR(80)  NULL,
    Region      NVARCHAR(80)  NULL,
    PostCode    NVARCHAR(20)  NULL,
    Country     NVARCHAR(60)  NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Store_Active DEFAULT (1),
    CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Store_Created DEFAULT (SYSUTCDATETIME()),
    RowVer      ROWVERSION    NOT NULL,
    CONSTRAINT UQ_Store_Code UNIQUE (StoreCode)
);
GO

CREATE TABLE ops.Vehicle
(
    VehicleId    INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicle PRIMARY KEY,
    Registration NVARCHAR(20) NOT NULL,
    Description  NVARCHAR(120) NULL,
    TrayCapacity INT          NULL,
    IsActive     BIT          NOT NULL CONSTRAINT DF_Vehicle_Active DEFAULT (1),
    CreatedUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_Vehicle_Created DEFAULT (SYSUTCDATETIME()),
    RowVer       ROWVERSION   NOT NULL,
    CONSTRAINT UQ_Vehicle_Reg UNIQUE (Registration)
);
GO

CREATE TABLE ops.Device
(
    DeviceId     INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Device PRIMARY KEY,
    DeviceCode   NVARCHAR(60) NOT NULL,       -- handheld id / edge module id / telematics id
    DeviceType   VARCHAR(20)  NOT NULL,       -- Handheld | EdgeCamera | Telematics | Api
    CheckpointId TINYINT      NULL CONSTRAINT FK_Device_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId),
    SiteCode     NVARCHAR(20) NULL,
    IsActive     BIT          NOT NULL CONSTRAINT DF_Device_Active DEFAULT (1),
    CreatedUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_Device_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Device_Code UNIQUE (DeviceCode),
    CONSTRAINT CK_Device_Type CHECK (DeviceType IN ('Handheld','EdgeCamera','Telematics','Api'))
);
GO

/* =====================================================================================
   ORDERS
   ===================================================================================== */

CREATE TABLE ops.SalesOrder
(
    SalesOrderId          BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
    OrderNumber           NVARCHAR(30) NOT NULL,
    StoreId               INT          NOT NULL CONSTRAINT FK_SalesOrder_Store REFERENCES ops.Store(StoreId),
    ErpReference          NVARCHAR(40) NULL,          -- D365 F&O sales order id
    OrderStatus           VARCHAR(20)  NOT NULL CONSTRAINT DF_SalesOrder_Status DEFAULT ('Open'),
    OrderDate             DATE         NULL,
    RequestedDeliveryDate DATE         NULL,
    CreatedUtc            DATETIME2(3) NOT NULL CONSTRAINT DF_SalesOrder_Created DEFAULT (SYSUTCDATETIME()),
    RowVer                ROWVERSION   NOT NULL,
    CONSTRAINT UQ_SalesOrder_Number UNIQUE (OrderNumber)
);
GO

CREATE TABLE ops.OrderLine
(
    OrderLineId         BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderLine PRIMARY KEY,
    SalesOrderId        BIGINT        NOT NULL CONSTRAINT FK_OrderLine_SalesOrder REFERENCES ops.SalesOrder(SalesOrderId),
    LineNumber          INT           NOT NULL,
    Gtin                CHAR(14)      NOT NULL,        -- GS1 GTIN-14
    ProductDescription  NVARCHAR(200) NULL,
    OrderedQty          DECIMAL(18,3) NOT NULL,
    Uom                 NVARCHAR(10)  NOT NULL CONSTRAINT DF_OrderLine_Uom DEFAULT ('EA'),
    ExpectedCartonCount INT           NOT NULL CONSTRAINT DF_OrderLine_ExpCtn DEFAULT (0),
    ErpLineReference    NVARCHAR(40)  NULL,
    CreatedUtc          DATETIME2(3)  NOT NULL CONSTRAINT DF_OrderLine_Created DEFAULT (SYSUTCDATETIME()),
    RowVer              ROWVERSION    NOT NULL,
    CONSTRAINT UQ_OrderLine UNIQUE (SalesOrderId, LineNumber),
    CONSTRAINT CK_OrderLine_Gtin CHECK (Gtin NOT LIKE '%[^0-9]%')   -- GTIN-14 must be all digits
);
GO

/* =====================================================================================
   CARTONS  (serialized GS1 identity)
   ===================================================================================== */

CREATE TABLE ops.Carton
(
    CartonId         BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_Carton PRIMARY KEY,
    OrderLineId      BIGINT        NOT NULL CONSTRAINT FK_Carton_OrderLine REFERENCES ops.OrderLine(OrderLineId),
    Gtin             CHAR(14)      NOT NULL,           -- (01) GTIN-14
    Serial           NVARCHAR(20)  NOT NULL,           -- (21) serial, <=20 alphanumeric
    Sscc             CHAR(18)      NULL,               -- optional SSCC-18 (00)
    Gs1ElementString NVARCHAR(120) NULL,               -- e.g. (01)0959...(21)ABC123
    QrPayload        NVARCHAR(200) NULL,               -- FNC1 01<gtin>21<serial>
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Carton_Status DEFAULT ('Expected'),
                     -- Expected | Picked | Staged | Loaded | Received | Exception
    CurrentTrayId    INT           NULL,               -- denormalized convenience (FK added after Tray)
    CreatedUtc       DATETIME2(3)  NOT NULL CONSTRAINT DF_Carton_Created DEFAULT (SYSUTCDATETIME()),
    RowVer           ROWVERSION    NOT NULL,
    CONSTRAINT UQ_Carton_GtinSerial UNIQUE (Gtin, Serial),
    CONSTRAINT CK_Carton_Gtin   CHECK (Gtin NOT LIKE '%[^0-9]%'),
    CONSTRAINT CK_Carton_Serial CHECK (Serial NOT LIKE '%[^0-9A-Za-z]%' AND LEN(Serial) BETWEEN 1 AND 20),
    CONSTRAINT CK_Carton_Sscc   CHECK (Sscc IS NULL OR (Sscc NOT LIKE '%[^0-9]%' AND LEN(Sscc) = 18))
);
GO
-- Filtered unique index: SSCC unique only when assigned.
CREATE UNIQUE INDEX UX_Carton_Sscc ON ops.Carton(Sscc) WHERE Sscc IS NOT NULL;
GO

/* =====================================================================================
   TRAYS  (reusable asset master)
   ===================================================================================== */

CREATE TABLE ops.Tray
(
    TrayId               INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tray PRIMARY KEY,
    TrayQr               NVARCHAR(30) NOT NULL,       -- TRAY-{siteCode}-{seq}
    SiteCode             NVARCHAR(20) NOT NULL,
    TrayStatus           VARCHAR(20)  NOT NULL CONSTRAINT DF_Tray_Status DEFAULT ('Available'),
                         -- Available | InUse | InTransit | AtStore | Maintenance | Lost | WrittenOff
    CurrentCustodianType VARCHAR(20)  NOT NULL CONSTRAINT DF_Tray_CustType DEFAULT ('Warehouse'),
                         -- Warehouse | Vehicle | Store
    CurrentCustodianRef  NVARCHAR(40) NULL,           -- site code / vehicle reg / store code
    LastSeenUtc          DATETIME2(3) NULL,
    CreatedUtc           DATETIME2(3) NOT NULL CONSTRAINT DF_Tray_Created DEFAULT (SYSUTCDATETIME()),
    RowVer               ROWVERSION   NOT NULL,
    CONSTRAINT UQ_Tray_Qr UNIQUE (TrayQr),
    CONSTRAINT CK_Tray_Status   CHECK (TrayStatus IN ('Available','InUse','InTransit','AtStore','Maintenance','Lost','WrittenOff')),
    CONSTRAINT CK_Tray_CustType CHECK (CurrentCustodianType IN ('Warehouse','Vehicle','Store'))
);
GO
-- Deferred FK from Carton.CurrentTrayId now that Tray exists.
ALTER TABLE ops.Carton
    ADD CONSTRAINT FK_Carton_Tray FOREIGN KEY (CurrentTrayId) REFERENCES ops.Tray(TrayId);
GO

/* =====================================================================================
   SCAN EVENTS  (append-only source of truth) — declared before binding tables that FK it.
   ===================================================================================== */

CREATE TABLE ops.ScanEvent
(
    ScanEventId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScanEvent PRIMARY KEY,
    EventType     VARCHAR(30)    NOT NULL CONSTRAINT FK_ScanEvent_EventType REFERENCES ref.EventType(EventTypeCode),
    CheckpointId  TINYINT        NULL CONSTRAINT FK_ScanEvent_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId),
    DeviceId      INT            NULL CONSTRAINT FK_ScanEvent_Device REFERENCES ops.Device(DeviceId),
    UserId        NVARCHAR(120)  NULL,
    ClientEventId NVARCHAR(60)   NOT NULL,        -- client-generated id for idempotency
    ScannedQr     NVARCHAR(200)  NULL,
    -- Optional subject references (any combination depending on event type):
    OrderLineId   BIGINT         NULL CONSTRAINT FK_ScanEvent_OrderLine REFERENCES ops.OrderLine(OrderLineId),
    CartonId      BIGINT         NULL CONSTRAINT FK_ScanEvent_Carton    REFERENCES ops.Carton(CartonId),
    TrayId        INT            NULL CONSTRAINT FK_ScanEvent_Tray       REFERENCES ops.Tray(TrayId),
    StoreId       INT            NULL CONSTRAINT FK_ScanEvent_Store      REFERENCES ops.Store(StoreId),
    TripId        BIGINT         NULL,            -- FK added after Trip exists
    Verdict       VARCHAR(20)    NULL,            -- PASS | COUNT_MISMATCH | UNKNOWN_CARTON | MISSING_CARTON | UNKNOWN
    PayloadJson   NVARCHAR(MAX)  NULL,            -- event-specific detail (decoded lists, counts, geo, etc.)
    EventUtc      DATETIME2(3)   NOT NULL,        -- timestamp asserted by the capturing device
    IngestedUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_ScanEvent_Ingested DEFAULT (SYSUTCDATETIME()),
    RowVer        ROWVERSION     NOT NULL,
    CONSTRAINT UQ_ScanEvent_Idem UNIQUE (DeviceId, ClientEventId),
    CONSTRAINT CK_ScanEvent_Payload CHECK (PayloadJson IS NULL OR ISJSON(PayloadJson) = 1)
);
GO

/* =====================================================================================
   TRAY CONTENT  (carton ⇆ tray binding with bind/unbind timestamps)
   ===================================================================================== */

CREATE TABLE ops.TrayContent
(
    TrayContentId     BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrayContent PRIMARY KEY,
    TrayId            INT          NOT NULL CONSTRAINT FK_TrayContent_Tray   REFERENCES ops.Tray(TrayId),
    CartonId          BIGINT       NOT NULL CONSTRAINT FK_TrayContent_Carton REFERENCES ops.Carton(CartonId),
    BoundUtc          DATETIME2(3) NOT NULL CONSTRAINT DF_TrayContent_Bound DEFAULT (SYSUTCDATETIME()),
    UnboundUtc        DATETIME2(3) NULL,
    BindScanEventId   BIGINT       NULL CONSTRAINT FK_TrayContent_BindEvt   REFERENCES ops.ScanEvent(ScanEventId),
    UnbindScanEventId BIGINT       NULL CONSTRAINT FK_TrayContent_UnbindEvt REFERENCES ops.ScanEvent(ScanEventId)
);
GO
-- A carton may be bound to at most one tray at a time.
CREATE UNIQUE INDEX UX_TrayContent_ActiveCarton ON ops.TrayContent(CartonId) WHERE UnboundUtc IS NULL;
GO

/* =====================================================================================
   TRIPS  (vehicle + route + manifest) and stops / loads
   ===================================================================================== */

CREATE TABLE ops.Trip
(
    TripId              BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_Trip PRIMARY KEY,
    TripNumber          NVARCHAR(30) NOT NULL,
    VehicleId           INT          NOT NULL CONSTRAINT FK_Trip_Vehicle REFERENCES ops.Vehicle(VehicleId),
    DriverName          NVARCHAR(120) NULL,
    DriverId            NVARCHAR(60)  NULL,
    RouteCode           NVARCHAR(30)  NULL,
    ManifestQr          NVARCHAR(50)  NULL,           -- trip manifest QR value
    TripStatus          VARCHAR(20)   NOT NULL CONSTRAINT DF_Trip_Status DEFAULT ('Planned'),
                        -- Planned | Loading | Loaded | Departed | Completed | Cancelled
    PlannedDepartureUtc DATETIME2(3)  NULL,
    ActualDepartureUtc  DATETIME2(3)  NULL,
    CompletedUtc        DATETIME2(3)  NULL,
    CreatedUtc          DATETIME2(3)  NOT NULL CONSTRAINT DF_Trip_Created DEFAULT (SYSUTCDATETIME()),
    RowVer              ROWVERSION    NOT NULL,
    CONSTRAINT UQ_Trip_Number UNIQUE (TripNumber),
    CONSTRAINT CK_Trip_Status CHECK (TripStatus IN ('Planned','Loading','Loaded','Departed','Completed','Cancelled'))
);
GO
-- Deferred FK from ScanEvent.TripId now that Trip exists.
ALTER TABLE ops.ScanEvent
    ADD CONSTRAINT FK_ScanEvent_Trip FOREIGN KEY (TripId) REFERENCES ops.Trip(TripId);
GO

CREATE TABLE ops.TripStop
(
    TripStopId   BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripStop PRIMARY KEY,
    TripId       BIGINT       NOT NULL CONSTRAINT FK_TripStop_Trip  REFERENCES ops.Trip(TripId),
    StoreId      INT          NOT NULL CONSTRAINT FK_TripStop_Store REFERENCES ops.Store(StoreId),
    StopSequence INT          NOT NULL,
    ArrivedUtc   DATETIME2(3) NULL,
    CompletedUtc DATETIME2(3) NULL,
    CONSTRAINT UQ_TripStop_Seq UNIQUE (TripId, StopSequence)
);
GO

CREATE TABLE ops.TripLoad
(
    TripLoadId      BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripLoad PRIMARY KEY,
    TripId          BIGINT       NOT NULL CONSTRAINT FK_TripLoad_Trip     REFERENCES ops.Trip(TripId),
    TrayId          INT          NOT NULL CONSTRAINT FK_TripLoad_Tray     REFERENCES ops.Tray(TrayId),
    TripStopId      BIGINT       NULL CONSTRAINT FK_TripLoad_Stop     REFERENCES ops.TripStop(TripStopId),
    IsPlanned       BIT          NOT NULL CONSTRAINT DF_TripLoad_Planned DEFAULT (1),
    LoadedUtc       DATETIME2(3) NULL,
    UnloadedUtc     DATETIME2(3) NULL,
    LoadScanEventId BIGINT       NULL CONSTRAINT FK_TripLoad_LoadEvt REFERENCES ops.ScanEvent(ScanEventId),
    CONSTRAINT UQ_TripLoad_TrayPerTrip UNIQUE (TripId, TrayId)
);
GO
-- A tray may be actively loaded on at most one trip at a time.
CREATE UNIQUE INDEX UX_TripLoad_ActiveTray ON ops.TripLoad(TrayId) WHERE UnloadedUtc IS NULL AND LoadedUtc IS NOT NULL;
GO

/* =====================================================================================
   TRAY CUSTODY  (ordered custody chain — every movement writes one row)
   ===================================================================================== */

CREATE TABLE ops.TrayCustody
(
    TrayCustodyId     BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrayCustody PRIMARY KEY,
    TrayId            INT          NOT NULL CONSTRAINT FK_TrayCustody_Tray REFERENCES ops.Tray(TrayId),
    FromCustodianType VARCHAR(20)  NULL,          -- Warehouse | Vehicle | Store (null = first sighting)
    FromCustodianRef  NVARCHAR(40) NULL,
    ToCustodianType   VARCHAR(20)  NOT NULL,      -- Warehouse | Vehicle | Store
    ToCustodianRef    NVARCHAR(40) NULL,
    CustodyUtc        DATETIME2(3) NOT NULL CONSTRAINT DF_TrayCustody_Utc DEFAULT (SYSUTCDATETIME()),
    TripId            BIGINT       NULL CONSTRAINT FK_TrayCustody_Trip  REFERENCES ops.Trip(TripId),
    ScanEventId       BIGINT       NULL CONSTRAINT FK_TrayCustody_Event REFERENCES ops.ScanEvent(ScanEventId),
    Note              NVARCHAR(200) NULL,
    CONSTRAINT CK_TrayCustody_From CHECK (FromCustodianType IS NULL OR FromCustodianType IN ('Warehouse','Vehicle','Store')),
    CONSTRAINT CK_TrayCustody_To   CHECK (ToCustodianType IN ('Warehouse','Vehicle','Store'))
);
GO

/* =====================================================================================
   SHIPMENT LINE STATE  (derived projection — one current row per order line)
   ===================================================================================== */

CREATE TABLE ops.ShipmentLineState
(
    OrderLineId    BIGINT       NOT NULL CONSTRAINT PK_ShipmentLineState PRIMARY KEY,  -- 1:1 with OrderLine
    CurrentState   VARCHAR(20)  NOT NULL CONSTRAINT FK_SLS_State  REFERENCES ref.ShipmentState(StateCode),
    PreviousState  VARCHAR(20)  NULL CONSTRAINT FK_SLS_PrevState REFERENCES ref.ShipmentState(StateCode),
    PickedCartons   INT         NOT NULL CONSTRAINT DF_SLS_Picked   DEFAULT (0),
    ReceivedCartons INT         NOT NULL CONSTRAINT DF_SLS_Received DEFAULT (0),
    LastEventId    BIGINT       NULL CONSTRAINT FK_SLS_LastEvent REFERENCES ops.ScanEvent(ScanEventId),
    StateEnteredUtc DATETIME2(3) NOT NULL CONSTRAINT DF_SLS_Entered DEFAULT (SYSUTCDATETIME()),
    RowVer         ROWVERSION   NOT NULL,
    CONSTRAINT FK_SLS_OrderLine FOREIGN KEY (OrderLineId) REFERENCES ops.OrderLine(OrderLineId)
);
GO

-- Optional audit trail of every state transition (append-only companion to the projection).
CREATE TABLE ops.ShipmentLineStateHistory
(
    HistoryId    BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_SLSHistory PRIMARY KEY,
    OrderLineId  BIGINT       NOT NULL CONSTRAINT FK_SLSH_OrderLine REFERENCES ops.OrderLine(OrderLineId),
    FromState    VARCHAR(20)  NULL,
    ToState      VARCHAR(20)  NOT NULL,
    ScanEventId  BIGINT       NULL CONSTRAINT FK_SLSH_Event REFERENCES ops.ScanEvent(ScanEventId),
    TransitionUtc DATETIME2(3) NOT NULL CONSTRAINT DF_SLSH_Utc DEFAULT (SYSUTCDATETIME()),
    WasLegal     BIT          NOT NULL CONSTRAINT DF_SLSH_Legal DEFAULT (1)
);
GO

/* =====================================================================================
   EXCEPTIONS
   ===================================================================================== */

CREATE TABLE ops.Exception
(
    ExceptionId        BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_Exception PRIMARY KEY,
    ExceptionType      VARCHAR(30)   NOT NULL,
        -- CountMismatch | UnknownCarton | MissingCarton | WrongTrip | WrongStore |
        -- IllegalTransition | TrayDwellExceeded | NoReceiveSla | SuspectedLost | Damaged | ShortShipped
    Severity           VARCHAR(10)   NOT NULL CONSTRAINT DF_Exception_Sev DEFAULT ('Medium'),
    Status             VARCHAR(15)   NOT NULL CONSTRAINT DF_Exception_Status DEFAULT ('Open'),
                       -- Open | Acknowledged | Escalated | Resolved
    CheckpointId       TINYINT       NULL CONSTRAINT FK_Exception_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId),
    OrderLineId        BIGINT        NULL CONSTRAINT FK_Exception_OrderLine  REFERENCES ops.OrderLine(OrderLineId),
    CartonId           BIGINT        NULL CONSTRAINT FK_Exception_Carton     REFERENCES ops.Carton(CartonId),
    TrayId             INT           NULL CONSTRAINT FK_Exception_Tray       REFERENCES ops.Tray(TrayId),
    TripId             BIGINT        NULL CONSTRAINT FK_Exception_Trip       REFERENCES ops.Trip(TripId),
    StoreId            INT           NULL CONSTRAINT FK_Exception_Store      REFERENCES ops.Store(StoreId),
    TriggeringEventId  BIGINT        NULL CONSTRAINT FK_Exception_Event      REFERENCES ops.ScanEvent(ScanEventId),
    Detail             NVARCHAR(400) NULL,
    FrameBlobUri       NVARCHAR(400) NULL,          -- dock annotated frame
    PhotoBlobUri       NVARCHAR(400) NULL,          -- receiving damage photo
    AcknowledgedByUser NVARCHAR(120) NULL,
    AcknowledgedUtc    DATETIME2(3)  NULL,
    ResolutionReason   NVARCHAR(60)  NULL,
    ResolutionNote     NVARCHAR(400) NULL,
    ResolvedByUser     NVARCHAR(120) NULL,
    ResolvedUtc        DATETIME2(3)  NULL,
    CreatedUtc         DATETIME2(3)  NOT NULL CONSTRAINT DF_Exception_Created DEFAULT (SYSUTCDATETIME()),
    RowVer             ROWVERSION    NOT NULL,
    CONSTRAINT CK_Exception_Sev    CHECK (Severity IN ('Low','Medium','High','Critical')),
    CONSTRAINT CK_Exception_Status CHECK (Status   IN ('Open','Acknowledged','Escalated','Resolved'))
);
GO

/* =====================================================================================
   APPEND-ONLY ENFORCEMENT ON ScanEvent
   Blocks UPDATE and DELETE so the event log is immutable. Inserts pass through.
   ===================================================================================== */

CREATE OR ALTER TRIGGER ops.TR_ScanEvent_NoMutate
ON ops.ScanEvent
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 53001, N'ops.ScanEvent is append-only: UPDATE and DELETE are not permitted.', 1;
END;
GO

/* =====================================================================================
   HOT-PATH INDEXES
   ===================================================================================== */

-- Lookup carton by scanned QR payload and by SSCC.
CREATE INDEX IX_Carton_QrPayload   ON ops.Carton(QrPayload)   WHERE QrPayload IS NOT NULL;
CREATE INDEX IX_Carton_OrderLine   ON ops.Carton(OrderLineId) INCLUDE (Status, CurrentTrayId);

-- Events by order line (timeline reconstruction) and by scanned QR.
CREATE INDEX IX_ScanEvent_OrderLine ON ops.ScanEvent(OrderLineId, EventUtc)
    INCLUDE (EventType, Verdict, CartonId, TrayId);
CREATE INDEX IX_ScanEvent_ScannedQr ON ops.ScanEvent(ScannedQr) WHERE ScannedQr IS NOT NULL;
CREATE INDEX IX_ScanEvent_Checkpoint_Ingested ON ops.ScanEvent(CheckpointId, IngestedUtc);
CREATE INDEX IX_ScanEvent_Tray ON ops.ScanEvent(TrayId, EventUtc) WHERE TrayId IS NOT NULL;

-- Open exceptions by checkpoint (ops console hot path).
CREATE INDEX IX_Exception_OpenByCheckpoint ON ops.Exception(CheckpointId, Severity, CreatedUtc)
    WHERE Status IN ('Open','Acknowledged','Escalated');

-- Shipment state lookups.
CREATE INDEX IX_SLS_State ON ops.ShipmentLineState(CurrentState) INCLUDE (StateEnteredUtc, LastEventId);

-- Tray custody chain by tray, newest first.
CREATE INDEX IX_TrayCustody_Tray ON ops.TrayCustody(TrayId, CustodyUtc DESC);

-- Trip load lookups.
CREATE INDEX IX_TripLoad_Trip ON ops.TripLoad(TripId) INCLUDE (TrayId, LoadedUtc, UnloadedUtc);
GO

PRINT N'TrackNTrash operational schema created.';
GO
