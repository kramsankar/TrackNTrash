/* =====================================================================================
   Migration 001 — Tray manifest cache for the edge dock verification + /manifests sync.
   The manifest is the expected contents of a tray (count + carton payloads), synced to the
   edge module. Kept as a small denormalized cache table (payloads as JSON).
   Idempotent. Run after 01_schema.sql.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'ops.TrayManifest', N'U') IS NULL
BEGIN
    CREATE TABLE ops.TrayManifest
    (
        TrayQr              NVARCHAR(30)  NOT NULL CONSTRAINT PK_TrayManifest PRIMARY KEY,
        TripId              BIGINT        NULL,
        ExpectedCartonCount INT           NOT NULL CONSTRAINT DF_TrayManifest_Count DEFAULT (0),
        ExpectedPayloadsJson NVARCHAR(MAX) NULL,   -- JSON array of carton QR payloads
        UpdatedUtc          DATETIME2(3)  NOT NULL CONSTRAINT DF_TrayManifest_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_TrayManifest_Json CHECK (ExpectedPayloadsJson IS NULL OR ISJSON(ExpectedPayloadsJson) = 1)
    );
    CREATE INDEX IX_TrayManifest_Updated ON ops.TrayManifest(UpdatedUtc);
    PRINT 'Created ops.TrayManifest';
END
ELSE PRINT 'ops.TrayManifest already exists';
GO
