/* =====================================================================================
   Migration 003 — Item-level tracking + vision cameras + site mapping.

   ITEMS
   -----
   An Item is an individual unit inside a carton. Two identification modes coexist:
     * Barcoded — the unit carries its own barcode/GTIN and is scanned individually.
     * Visual   — the unit is unlabelled; a camera counts it. No per-unit identity,
                  only a count, so visual items are represented by the carton's
                  ExpectedItemCount plus counted totals per checkpoint.
   ItemCount records what was observed at a checkpoint (scanned and/or vision), so the
   same reconciliation pattern used for cartons applies one level down.

   CAMERAS
   -------
   Camera holds identity, kind (Fixed | Handheld), connection config and a structured
   location (site → zone → station). CameraPlacement pins a camera to an (x, y) point
   on a SiteMap floor plan — coordinates are stored as 0..1 fractions so the map image
   can be re-rendered at any size.

   Idempotent. Run after 01_schema.sql.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------- Items ---- */

-- Per-carton expectation of how many units it should hold, and how they are identified.
IF COL_LENGTH('ops.Carton', 'ExpectedItemCount') IS NULL
BEGIN
    ALTER TABLE ops.Carton ADD ExpectedItemCount INT NOT NULL CONSTRAINT DF_Carton_ExpItems DEFAULT (0);
    PRINT 'Added ops.Carton.ExpectedItemCount';
END
GO
IF COL_LENGTH('ops.Carton', 'ItemIdentification') IS NULL
BEGIN
    ALTER TABLE ops.Carton ADD ItemIdentification VARCHAR(10) NOT NULL
        CONSTRAINT DF_Carton_ItemIdent DEFAULT ('Visual');   -- Barcoded | Visual | Mixed
    PRINT 'Added ops.Carton.ItemIdentification';
END
GO

-- Individually identified (barcoded) units. Visual-only cartons have no rows here.
IF OBJECT_ID(N'ops.Item', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Item
    (
        ItemId      BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_Item PRIMARY KEY,
        CartonId    BIGINT        NOT NULL CONSTRAINT FK_Item_Carton REFERENCES ops.Carton(CartonId),
        Barcode     NVARCHAR(60)  NOT NULL,          -- GTIN, EAN, serial — whatever the unit carries
        Gtin        CHAR(14)      NULL,
        Description NVARCHAR(200) NULL,
        Status      VARCHAR(20)   NOT NULL CONSTRAINT DF_Item_Status DEFAULT ('Expected'),
                    -- Expected | Picked | Verified | Received | Missing | Damaged | Unexpected
        CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Item_Created DEFAULT (SYSUTCDATETIME()),
        RowVer      ROWVERSION    NOT NULL,
        CONSTRAINT UQ_Item_CartonBarcode UNIQUE (CartonId, Barcode)
    );
    CREATE INDEX IX_Item_Barcode ON ops.Item(Barcode);
    CREATE INDEX IX_Item_Carton ON ops.Item(CartonId) INCLUDE (Status);
    PRINT 'Created ops.Item';
END
ELSE PRINT 'ops.Item already exists';
GO

-- What was actually observed for a carton at a checkpoint: units scanned and/or counted
-- by a camera, against what was expected. One row per (carton, checkpoint) observation.
IF OBJECT_ID(N'ops.ItemCount', N'U') IS NULL
BEGIN
    CREATE TABLE ops.ItemCount
    (
        ItemCountId   BIGINT       IDENTITY(1,1) NOT NULL CONSTRAINT PK_ItemCount PRIMARY KEY,
        CartonId      BIGINT       NOT NULL CONSTRAINT FK_ItemCount_Carton REFERENCES ops.Carton(CartonId),
        CheckpointId  TINYINT      NULL CONSTRAINT FK_ItemCount_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId),
        ExpectedCount INT          NOT NULL,
        ScannedCount  INT          NOT NULL CONSTRAINT DF_ItemCount_Scanned DEFAULT (0),
        VisionCount   INT          NULL,          -- null when no camera observed this carton
        CameraId      INT          NULL,          -- FK added after ops.Camera exists
        Verdict       VARCHAR(20)  NOT NULL,      -- MATCH | SHORT | OVER | UNVERIFIED
        FrameBlobUri  NVARCHAR(400) NULL,         -- annotated inspection frame
        Confidence    DECIMAL(5,4) NULL,          -- model confidence for the visual count
        ScanEventId   BIGINT       NULL CONSTRAINT FK_ItemCount_Event REFERENCES ops.ScanEvent(ScanEventId),
        ObservedUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_ItemCount_Observed DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ItemCount_Verdict CHECK (Verdict IN ('MATCH','SHORT','OVER','UNVERIFIED'))
    );
    CREATE INDEX IX_ItemCount_Carton ON ops.ItemCount(CartonId, ObservedUtc DESC);
    CREATE INDEX IX_ItemCount_Verdict ON ops.ItemCount(Verdict, ObservedUtc DESC) WHERE Verdict <> 'MATCH';
    PRINT 'Created ops.ItemCount';
END
ELSE PRINT 'ops.ItemCount already exists';
GO

/* -------------------------------------------------------------- Cameras ---- */

IF OBJECT_ID(N'ops.SiteMap', N'U') IS NULL
BEGIN
    CREATE TABLE ops.SiteMap
    (
        SiteMapId   INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_SiteMap PRIMARY KEY,
        SiteCode    NVARCHAR(20)  NOT NULL,
        Name        NVARCHAR(120) NOT NULL,
        ImageUri    NVARCHAR(400) NULL,           -- floor-plan image (blob); null = plain grid
        Width       INT           NOT NULL CONSTRAINT DF_SiteMap_W DEFAULT (1000),
        Height      INT           NOT NULL CONSTRAINT DF_SiteMap_H DEFAULT (600),
        CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_SiteMap_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_SiteMap_Site UNIQUE (SiteCode)
    );
    PRINT 'Created ops.SiteMap';
END
ELSE PRINT 'ops.SiteMap already exists';
GO

IF OBJECT_ID(N'ops.Camera', N'U') IS NULL
BEGIN
    CREATE TABLE ops.Camera
    (
        CameraId     INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Camera PRIMARY KEY,
        CameraCode   NVARCHAR(40)  NOT NULL,      -- CAM-LDN1-DOCK-01
        Name         NVARCHAR(120) NOT NULL,
        CameraKind   VARCHAR(12)   NOT NULL CONSTRAINT DF_Camera_Kind DEFAULT ('Fixed'),  -- Fixed | Handheld
        -- structured location
        SiteCode     NVARCHAR(20)  NOT NULL,
        Zone         NVARCHAR(60)  NULL,          -- e.g. Dispatch, Pick Face, Goods In
        Station      NVARCHAR(60)  NULL,          -- e.g. Dock Door 3, Pack Bench 2
        CheckpointId TINYINT       NULL CONSTRAINT FK_Camera_Checkpoint REFERENCES ref.[Checkpoint](CheckpointId),
        -- connection / capability
        RtspUrl      NVARCHAR(400) NULL,          -- null for handheld (phone camera)
        DeviceId     INT           NULL CONSTRAINT FK_Camera_Device REFERENCES ops.Device(DeviceId),
        Purpose      VARCHAR(20)   NOT NULL CONSTRAINT DF_Camera_Purpose DEFAULT ('ItemCount'),
                     -- ItemCount | CartonVerify | Both
        Status       VARCHAR(15)   NOT NULL CONSTRAINT DF_Camera_Status DEFAULT ('Active'),
                     -- Active | Offline | Maintenance | Retired
        LastSeenUtc  DATETIME2(3)  NULL,
        CreatedUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Camera_Created DEFAULT (SYSUTCDATETIME()),
        RowVer       ROWVERSION    NOT NULL,
        CONSTRAINT UQ_Camera_Code UNIQUE (CameraCode),
        CONSTRAINT CK_Camera_Kind    CHECK (CameraKind IN ('Fixed','Handheld')),
        CONSTRAINT CK_Camera_Purpose CHECK (Purpose IN ('ItemCount','CartonVerify','Both')),
        CONSTRAINT CK_Camera_Status  CHECK (Status IN ('Active','Offline','Maintenance','Retired'))
    );
    CREATE INDEX IX_Camera_Site ON ops.Camera(SiteCode, Zone);
    PRINT 'Created ops.Camera';
END
ELSE PRINT 'ops.Camera already exists';
GO

-- Deferred FK: item counts reference the camera that produced the visual count.
IF OBJECT_ID('FK_ItemCount_Camera', 'F') IS NULL AND OBJECT_ID('ops.Camera','U') IS NOT NULL
BEGIN
    ALTER TABLE ops.ItemCount ADD CONSTRAINT FK_ItemCount_Camera FOREIGN KEY (CameraId) REFERENCES ops.Camera(CameraId);
    PRINT 'Added FK_ItemCount_Camera';
END
GO

-- Where a camera sits on the floor plan. Coordinates are 0..1 fractions of the map,
-- so the same placement renders correctly at any display size.
IF OBJECT_ID(N'ops.CameraPlacement', N'U') IS NULL
BEGIN
    CREATE TABLE ops.CameraPlacement
    (
        PlacementId  INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_CameraPlacement PRIMARY KEY,
        CameraId     INT           NOT NULL CONSTRAINT FK_Placement_Camera REFERENCES ops.Camera(CameraId),
        SiteMapId    INT           NOT NULL CONSTRAINT FK_Placement_SiteMap REFERENCES ops.SiteMap(SiteMapId),
        X            DECIMAL(9,6)  NOT NULL,      -- 0..1 across the map
        Y            DECIMAL(9,6)  NOT NULL,      -- 0..1 down the map
        HeadingDeg   INT           NULL,          -- optional facing direction, 0 = north
        UpdatedUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Placement_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Placement_Camera UNIQUE (CameraId, SiteMapId),
        CONSTRAINT CK_Placement_X CHECK (X BETWEEN 0 AND 1),
        CONSTRAINT CK_Placement_Y CHECK (Y BETWEEN 0 AND 1)
    );
    PRINT 'Created ops.CameraPlacement';
END
ELSE PRINT 'ops.CameraPlacement already exists';
GO

/* ---- New event types for item-level observations ---- */
MERGE ref.EventType AS t
USING (VALUES
    ('ItemScan',        N'Individual item scanned',        NULL),
    ('ItemVisionCount', N'Items counted by camera',        NULL),
    ('ItemCountComplete', N'Item count reconciled',        NULL)
) AS s(EventTypeCode, Name, CheckpointId)
ON t.EventTypeCode = s.EventTypeCode
WHEN NOT MATCHED THEN INSERT (EventTypeCode, Name, CheckpointId)
    VALUES (s.EventTypeCode, s.Name, s.CheckpointId);
GO

PRINT 'Migration 003 complete.';
GO
