/* =====================================================================================
   Migration 008 — Persist in-progress receiving sessions.

   A receiving session is the state of a store colleague working through a tray at the
   door: which cartons are already in, which arrived that shouldn't have, which are
   damaged. It was held in a dictionary, so an App Service recycle mid-round lost the
   lot and the colleague had to start the tray again from the first carton.

   The session id sequence restarted at recv-000001 too, so a retried request carrying
   an id from before the recycle could land on somebody else's tray. SessionId is stored
   as the natural key here and issued from a table-backed sequence, so ids are never
   reused.

   Sessions are deleted on completion — the outcome lives in ops.ScanEvent and the
   receiving summary, not here. Only in-flight work is stored.

   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('ops.ReceivingSession', 'U') IS NULL
BEGIN
    CREATE TABLE ops.ReceivingSession
    (
        ReceivingSessionId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ReceivingSession PRIMARY KEY,
        SessionId   NVARCHAR(40)  NOT NULL CONSTRAINT UX_ReceivingSession_SessionId UNIQUE,
        TrayQr      NVARCHAR(64)  NOT NULL,
        StoreCode   NVARCHAR(20)  NOT NULL,
        StartedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_ReceivingSession_Started DEFAULT SYSUTCDATETIME(),
        UpdatedUtc  DATETIME2(3)  NULL
    );
    CREATE INDEX IX_ReceivingSession_Tray ON ops.ReceivingSession (TrayQr, StoreCode);
    PRINT 'Created ops.ReceivingSession';
END
ELSE PRINT 'ops.ReceivingSession already exists';
GO

/* ---------------------------------------------------------------------------------
   One row per carton the colleague has dispositioned. Outcome is Received / Over /
   Damaged — the three buckets the session tracks. A carton can only be in one bucket,
   which the unique constraint enforces so a duplicate scan cannot double-count.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.ReceivingSessionScan', 'U') IS NULL
BEGIN
    CREATE TABLE ops.ReceivingSessionScan
    (
        ReceivingSessionScanId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ReceivingSessionScan PRIMARY KEY,
        ReceivingSessionId BIGINT NOT NULL
            CONSTRAINT FK_ReceivingSessionScan_Session
            FOREIGN KEY REFERENCES ops.ReceivingSession(ReceivingSessionId) ON DELETE CASCADE,
        Payload   NVARCHAR(128) NOT NULL,
        Outcome   VARCHAR(12)   NOT NULL
            CONSTRAINT CK_ReceivingSessionScan_Outcome
            CHECK (Outcome IN ('Received', 'Over', 'Damaged')),
        ScannedUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_ReceivingSessionScan_Utc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_ReceivingSessionScan_Payload UNIQUE (ReceivingSessionId, Payload)
    );
    PRINT 'Created ops.ReceivingSessionScan';
END
ELSE PRINT 'ops.ReceivingSessionScan already exists';
GO

/* ---------------------------------------------------------------------------------
   Session id sequence. A SEQUENCE rather than IDENTITY so the number can be read and
   formatted (recv-000123) before the row is written, and so it never rewinds.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.ReceivingSessionSeq', 'SO') IS NULL
BEGIN
    CREATE SEQUENCE ops.ReceivingSessionSeq AS BIGINT START WITH 1 INCREMENT BY 1;
    PRINT 'Created ops.ReceivingSessionSeq';
END
ELSE PRINT 'ops.ReceivingSessionSeq already exists';
GO

PRINT 'Migration 008 complete.';
GO
