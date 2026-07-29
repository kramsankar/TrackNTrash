/* =====================================================================================
   Migration 007 — Persist advance shipping notices and the exception audit trail.

   Two stores were still in memory and lost their contents on every App Service recycle:

   * ASNs. A store cannot receive a tray whose ASN has vanished, so a recycle mid-round
     stranded deliveries at the door with no expected-carton list to check against.
   * The console's exception list. ops.Exception already held the rows, but the console
     read a separate in-memory copy that started empty, so a restart showed a clean
     board while real exceptions sat unactioned in the table.

   ops.ExceptionAudit records who acknowledged/resolved/escalated and when. ops.Exception
   has AcknowledgedByUser/ResolvedByUser columns, but those hold only the latest actor and
   cannot represent an escalation or a repeated action, which is exactly what the audit
   trail on the console shows.

   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------------
   ASN header. Keyed by (TrayQr, StoreCode) — the same key the receiving app looks up.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.Asn', 'U') IS NULL
BEGIN
    CREATE TABLE ops.Asn
    (
        AsnId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Asn PRIMARY KEY,
        TrayQr      NVARCHAR(64)  NOT NULL,
        StoreCode   NVARCHAR(20)  NOT NULL,
        CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Asn_Created DEFAULT SYSUTCDATETIME(),
        UpdatedUtc  DATETIME2(3)  NULL,
        CONSTRAINT UX_Asn_TrayStore UNIQUE (TrayQr, StoreCode)
    );
    PRINT 'Created ops.Asn';
END
ELSE PRINT 'ops.Asn already exists';
GO

/* ---------------------------------------------------------------------------------
   Expected cartons. Replaced wholesale on upsert, so no natural key beyond the parent.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.AsnLine', 'U') IS NULL
BEGIN
    CREATE TABLE ops.AsnLine
    (
        AsnLineId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AsnLine PRIMARY KEY,
        AsnId        INT           NOT NULL
            CONSTRAINT FK_AsnLine_Asn FOREIGN KEY REFERENCES ops.Asn(AsnId) ON DELETE CASCADE,
        Payload      NVARCHAR(128) NOT NULL,
        OrderLineId  BIGINT        NOT NULL,
        Gtin         NVARCHAR(14)  NULL
    );
    -- Over-scan resolution asks "which store does this carton belong to?" by payload.
    CREATE INDEX IX_AsnLine_Payload ON ops.AsnLine (Payload) INCLUDE (AsnId);
    CREATE INDEX IX_AsnLine_Asn     ON ops.AsnLine (AsnId);
    PRINT 'Created ops.AsnLine';
END
ELSE PRINT 'ops.AsnLine already exists';
GO

/* ---------------------------------------------------------------------------------
   Exception audit trail — one row per console action, oldest first when replayed.
   --------------------------------------------------------------------------------- */
IF OBJECT_ID('ops.ExceptionAudit', 'U') IS NULL
BEGIN
    CREATE TABLE ops.ExceptionAudit
    (
        ExceptionAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExceptionAudit PRIMARY KEY,
        ExceptionId      BIGINT        NOT NULL
            CONSTRAINT FK_ExceptionAudit_Exception FOREIGN KEY REFERENCES ops.Exception(ExceptionId),
        Action           NVARCHAR(30)  NOT NULL,
        ActionedByUser   NVARCHAR(120) NOT NULL,
        Note             NVARCHAR(500) NULL,
        ActionedUtc      DATETIME2(3)  NOT NULL CONSTRAINT DF_ExceptionAudit_Utc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ExceptionAudit_Exception ON ops.ExceptionAudit (ExceptionId, ExceptionAuditId);
    PRINT 'Created ops.ExceptionAudit';
END
ELSE PRINT 'ops.ExceptionAudit already exists';
GO

PRINT 'Migration 007 complete.';
GO
