/* =====================================================================================
   TrackNTrash — Module 11: Analytics mart (star schema) in Azure SQL.
   Separate `mart` schema fed by incremental loads from the operational tables (Module 1).
   Run after 01_schema.sql.
   ===================================================================================== */
SET NOCOUNT ON;
GO
IF SCHEMA_ID(N'mart') IS NULL EXEC(N'CREATE SCHEMA mart AUTHORIZATION dbo;');
GO

/* ---------------- Dimensions ---------------- */

CREATE TABLE mart.DimDate
(
    DateKey     INT          NOT NULL CONSTRAINT PK_DimDate PRIMARY KEY,  -- yyyymmdd
    [Date]      DATE         NOT NULL,
    [Year]      SMALLINT     NOT NULL,
    [Quarter]   TINYINT      NOT NULL,
    [Month]     TINYINT      NOT NULL,
    MonthName   NVARCHAR(20) NOT NULL,
    [Day]       TINYINT      NOT NULL,
    DayOfWeek   TINYINT      NOT NULL,
    DayName     NVARCHAR(20) NOT NULL,
    IsWeekend   BIT          NOT NULL,
    YearMonth   CHAR(7)      NOT NULL
);
GO

CREATE TABLE mart.DimStore
(
    StoreKey  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimStore PRIMARY KEY,
    StoreId   INT           NOT NULL,
    StoreCode NVARCHAR(20)  NOT NULL,
    StoreName NVARCHAR(120) NULL,
    Region    NVARCHAR(80)  NULL,
    CONSTRAINT UQ_DimStore UNIQUE (StoreId)
);
GO

CREATE TABLE mart.DimRoute
(
    RouteKey  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimRoute PRIMARY KEY,
    RouteCode NVARCHAR(30) NOT NULL,
    CONSTRAINT UQ_DimRoute UNIQUE (RouteCode)
);
GO

CREATE TABLE mart.DimVehicle
(
    VehicleKey   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimVehicle PRIMARY KEY,
    VehicleId    INT          NOT NULL,
    Registration NVARCHAR(20) NOT NULL,
    CONSTRAINT UQ_DimVehicle UNIQUE (VehicleId)
);
GO

CREATE TABLE mart.DimProduct
(
    ProductKey  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimProduct PRIMARY KEY,
    Gtin        CHAR(14)      NOT NULL,
    Description NVARCHAR(200) NULL,
    CONSTRAINT UQ_DimProduct UNIQUE (Gtin)
);
GO

CREATE TABLE mart.DimCheckpoint
(
    CheckpointKey  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimCheckpoint PRIMARY KEY,
    CheckpointId   TINYINT      NOT NULL,
    CheckpointCode VARCHAR(20)  NOT NULL,
    CheckpointName NVARCHAR(60) NOT NULL,
    CONSTRAINT UQ_DimCheckpoint UNIQUE (CheckpointId)
);
GO

/* ---------------- Facts ---------------- */

-- Grain: one row per scan/verification event.
CREATE TABLE mart.FactScanEvent
(
    ScanEventId   BIGINT       NOT NULL CONSTRAINT PK_FactScanEvent PRIMARY KEY,
    DateKey       INT          NOT NULL CONSTRAINT FK_FSE_Date       REFERENCES mart.DimDate(DateKey),
    CheckpointKey INT          NULL CONSTRAINT FK_FSE_Checkpoint REFERENCES mart.DimCheckpoint(CheckpointKey),
    StoreKey      INT          NULL CONSTRAINT FK_FSE_Store      REFERENCES mart.DimStore(StoreKey),
    ProductKey    INT          NULL CONSTRAINT FK_FSE_Product    REFERENCES mart.DimProduct(ProductKey),
    EventType     VARCHAR(30)  NOT NULL,
    Verdict       VARCHAR(20)  NULL,
    IsFirstScanMatch BIT       NULL,          -- carton matched order line on first scan
    EventUtc      DATETIME2(3) NOT NULL,
    IngestLatencyMs INT        NULL
);
GO
CREATE INDEX IX_FSE_Date ON mart.FactScanEvent(DateKey);

-- Grain: one row per order line (current terminal outcome).
CREATE TABLE mart.FactShipmentLine
(
    OrderLineId   BIGINT       NOT NULL CONSTRAINT PK_FactShipmentLine PRIMARY KEY,
    DateKey       INT          NOT NULL CONSTRAINT FK_FSL_Date    REFERENCES mart.DimDate(DateKey),
    StoreKey      INT          NOT NULL CONSTRAINT FK_FSL_Store   REFERENCES mart.DimStore(StoreKey),
    ProductKey    INT          NULL CONSTRAINT FK_FSL_Product REFERENCES mart.DimProduct(ProductKey),
    ExpectedCartons INT        NOT NULL,
    ReceivedCartons INT        NOT NULL,
    FinalState    VARCHAR(20)  NOT NULL,
    IsReceivedClean BIT        NOT NULL,      -- received with no short/over/damage
    OnTime        BIT          NULL,          -- received within SLA of loaded
    ShippedUtc    DATETIME2(3) NULL,
    ReceivedUtc   DATETIME2(3) NULL
);
GO
CREATE INDEX IX_FSL_Store ON mart.FactShipmentLine(StoreKey, DateKey);

-- Grain: one row per exception.
CREATE TABLE mart.FactException
(
    ExceptionId   BIGINT       NOT NULL CONSTRAINT PK_FactException PRIMARY KEY,
    DateKey       INT          NOT NULL CONSTRAINT FK_FE_Date       REFERENCES mart.DimDate(DateKey),
    CheckpointKey INT          NULL CONSTRAINT FK_FE_Checkpoint REFERENCES mart.DimCheckpoint(CheckpointKey),
    StoreKey      INT          NULL CONSTRAINT FK_FE_Store      REFERENCES mart.DimStore(StoreKey),
    ExceptionType VARCHAR(30)  NOT NULL,
    Severity      VARCHAR(10)  NOT NULL,
    Status        VARCHAR(15)  NOT NULL,
    ResolutionMinutes INT      NULL,
    CreatedUtc    DATETIME2(3) NOT NULL
);
GO
CREATE INDEX IX_FE_Date ON mart.FactException(DateKey, CheckpointKey);

-- Grain: one row per tray-trip (utilization / loss).
CREATE TABLE mart.FactTrayTrip
(
    TrayTripId   BIGINT       NOT NULL CONSTRAINT PK_FactTrayTrip PRIMARY KEY,
    DateKey      INT          NOT NULL CONSTRAINT FK_FTT_Date    REFERENCES mart.DimDate(DateKey),
    RouteKey     INT          NULL CONSTRAINT FK_FTT_Route   REFERENCES mart.DimRoute(RouteKey),
    VehicleKey   INT          NULL CONSTRAINT FK_FTT_Vehicle REFERENCES mart.DimVehicle(VehicleKey),
    StoreKey     INT          NULL CONSTRAINT FK_FTT_Store   REFERENCES mart.DimStore(StoreKey),
    TrayId       INT          NOT NULL,
    Returned     BIT          NOT NULL,
    DwellHours   DECIMAL(10,2) NULL,
    DepartedUtc  DATETIME2(3) NULL,
    ReturnedUtc  DATETIME2(3) NULL
);
GO
CREATE INDEX IX_FTT_Date ON mart.FactTrayTrip(DateKey, RouteKey);

/* ETL watermark table for incremental loads. */
CREATE TABLE mart.EtlWatermark
(
    TableName    NVARCHAR(60) NOT NULL CONSTRAINT PK_EtlWatermark PRIMARY KEY,
    LastLoadedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Etl_Last DEFAULT ('2000-01-01')
);
GO

PRINT N'TrackNTrash analytics mart created.';
GO
