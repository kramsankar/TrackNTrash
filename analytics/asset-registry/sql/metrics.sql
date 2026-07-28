/* =====================================================================================
   TrackNTrash — Module 10: Asset (tray) metrics tables + nightly computation procedures.
   Depends on Module 1 schema (ops.Tray, ops.TrayCustody, ops.TripLoad, ops.Trip,
   ops.ScanEvent, ops.Exception). Run after 01_schema.sql.
   ===================================================================================== */
SET NOCOUNT ON;
GO
IF SCHEMA_ID(N'asset') IS NULL EXEC(N'CREATE SCHEMA asset AUTHORIZATION dbo;');
GO

/* ---- Metric output tables (snapshotted nightly) ---------------------------------- */

CREATE TABLE asset.TrayUtilization
(
    SnapshotDate   DATE         NOT NULL,
    TrayId         INT          NOT NULL,
    YearMonth      CHAR(7)      NOT NULL,      -- 'YYYY-MM'
    TripCount      INT          NOT NULL,
    CONSTRAINT PK_TrayUtilization PRIMARY KEY (SnapshotDate, TrayId, YearMonth)
);
GO

CREATE TABLE asset.TrayDwell
(
    SnapshotDate   DATE         NOT NULL,
    TrayId         INT          NOT NULL,
    CustodianType  VARCHAR(20)  NOT NULL,
    CustodianRef   NVARCHAR(40) NULL,
    DwellHours     DECIMAL(10,2) NOT NULL,
    ExceededFlag   BIT          NOT NULL,
    CONSTRAINT PK_TrayDwell PRIMARY KEY (SnapshotDate, TrayId, CustodianType, CustodianRef)
);
GO

CREATE TABLE asset.LossRate
(
    SnapshotDate   DATE         NOT NULL,
    Dimension      VARCHAR(10)  NOT NULL,      -- 'Route' | 'Store'
    DimensionKey   NVARCHAR(40) NOT NULL,
    Sent           INT          NOT NULL,
    NotReturned    INT          NOT NULL,
    LossRatePct    DECIMAL(6,2) NOT NULL,
    CONSTRAINT PK_LossRate PRIMARY KEY (SnapshotDate, Dimension, DimensionKey)
);
GO

CREATE TABLE asset.FleetRecommendation
(
    SnapshotDate      DATE          NOT NULL CONSTRAINT PK_FleetRec PRIMARY KEY,
    CirculatingTrays  INT           NOT NULL,
    AvgCycleDays      DECIMAL(6,2)  NOT NULL,
    DailyDemandTrays  DECIMAL(10,2) NOT NULL,
    TargetUtilization DECIMAL(4,2)  NOT NULL,
    RecommendedFleet  INT           NOT NULL
);
GO

/* =====================================================================================
   PROC: asset.usp_ComputeNightlyMetrics
   @DwellThresholdDays  — dwell above this at any location is flagged
   @UnseenLostDays      — tray unseen this many days => SuspectedLost exception
   @TargetUtilization   — target fraction (0..1) used in fleet sizing
   ===================================================================================== */
CREATE OR ALTER PROCEDURE asset.usp_ComputeNightlyMetrics
    @DwellThresholdDays INT = 3,
    @UnseenLostDays     INT = 21,
    @TargetUtilization  DECIMAL(4,2) = 0.80
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);

    /* ---- Utilization: distinct trips per tray per month (last 12 months) ---- */
    DELETE FROM asset.TrayUtilization WHERE SnapshotDate = @today;
    INSERT INTO asset.TrayUtilization (SnapshotDate, TrayId, YearMonth, TripCount)
    SELECT @today, tl.TrayId,
           FORMAT(t.ActualDepartureUtc, 'yyyy-MM'),
           COUNT(DISTINCT tl.TripId)
    FROM ops.TripLoad tl
    JOIN ops.Trip t ON t.TripId = tl.TripId
    WHERE t.ActualDepartureUtc >= DATEADD(MONTH, -12, @today)
      AND tl.LoadedUtc IS NOT NULL
    GROUP BY tl.TrayId, FORMAT(t.ActualDepartureUtc, 'yyyy-MM');

    /* ---- Dwell: hours between consecutive custody events at each location ---- */
    DELETE FROM asset.TrayDwell WHERE SnapshotDate = @today;
    ;WITH custody AS (
        SELECT TrayId, ToCustodianType AS CustodianType, ToCustodianRef AS CustodianRef,
               CustodyUtc,
               LEAD(CustodyUtc) OVER (PARTITION BY TrayId ORDER BY CustodyUtc) AS NextUtc
        FROM ops.TrayCustody
    )
    INSERT INTO asset.TrayDwell (SnapshotDate, TrayId, CustodianType, CustodianRef, DwellHours, ExceededFlag)
    SELECT @today, TrayId, CustodianType, ISNULL(CustodianRef, ''),
           CAST(DATEDIFF(MINUTE, CustodyUtc, ISNULL(NextUtc, SYSUTCDATETIME())) / 60.0 AS DECIMAL(10,2)),
           CASE WHEN DATEDIFF(HOUR, CustodyUtc, ISNULL(NextUtc, SYSUTCDATETIME())) > @DwellThresholdDays * 24
                THEN 1 ELSE 0 END
    FROM custody;

    /* ---- Suspected lost: not seen in @UnseenLostDays -> raise exception (dedup by open) ---- */
    INSERT INTO ops.Exception (ExceptionType, Severity, TrayId, Detail, CreatedUtc)
    SELECT 'SuspectedLost', 'Medium', t.TrayId,
           CONCAT('Tray ', t.TrayQr, ' not seen since ', CONVERT(varchar(19), t.LastSeenUtc, 126)),
           SYSUTCDATETIME()
    FROM ops.Tray t
    WHERE t.TrayStatus NOT IN ('Lost','WrittenOff')
      AND (t.LastSeenUtc IS NULL OR t.LastSeenUtc < DATEADD(DAY, -@UnseenLostDays, SYSUTCDATETIME()))
      AND NOT EXISTS (
          SELECT 1 FROM ops.Exception e
          WHERE e.TrayId = t.TrayId AND e.ExceptionType = 'SuspectedLost'
            AND e.Status IN ('Open','Acknowledged','Escalated'));

    /* ---- Loss rate by route and by store ----
       A tray is "not returned" if its latest custody is still Store/Vehicle for a trip
       older than the SLA window (approximated by trips departed > @UnseenLostDays ago
       with no subsequent Warehouse custody). */
    DELETE FROM asset.LossRate WHERE SnapshotDate = @today;

    ;WITH sent AS (
        SELECT t.RouteCode, s.StoreCode, tl.TrayId, t.TripId, t.ActualDepartureUtc
        FROM ops.TripLoad tl
        JOIN ops.Trip t      ON t.TripId = tl.TripId
        JOIN ops.TripStop ts ON ts.TripStopId = tl.TripStopId
        JOIN ops.Store s     ON s.StoreId = ts.StoreId
        WHERE t.ActualDepartureUtc >= DATEADD(DAY, -90, @today)
    ),
    returned AS (
        SELECT DISTINCT tc.TrayId
        FROM ops.TrayCustody tc
        WHERE tc.ToCustodianType = 'Warehouse'
    )
    INSERT INTO asset.LossRate (SnapshotDate, Dimension, DimensionKey, Sent, NotReturned, LossRatePct)
    SELECT @today, 'Route', ISNULL(RouteCode,'(none)'),
           COUNT(DISTINCT TrayId),
           COUNT(DISTINCT CASE WHEN TrayId NOT IN (SELECT TrayId FROM returned) THEN TrayId END),
           CAST(100.0 * COUNT(DISTINCT CASE WHEN TrayId NOT IN (SELECT TrayId FROM returned) THEN TrayId END)
                / NULLIF(COUNT(DISTINCT TrayId),0) AS DECIMAL(6,2))
    FROM sent GROUP BY RouteCode
    UNION ALL
    SELECT @today, 'Store', StoreCode,
           COUNT(DISTINCT TrayId),
           COUNT(DISTINCT CASE WHEN TrayId NOT IN (SELECT TrayId FROM returned) THEN TrayId END),
           CAST(100.0 * COUNT(DISTINCT CASE WHEN TrayId NOT IN (SELECT TrayId FROM returned) THEN TrayId END)
                / NULLIF(COUNT(DISTINCT TrayId),0) AS DECIMAL(6,2))
    FROM sent GROUP BY StoreCode;

    /* ---- Fleet sizing: recommended = ceil(daily demand * cycle days / target utilization) ---- */
    DECLARE @circulating INT =
        (SELECT COUNT(*) FROM ops.Tray WHERE TrayStatus NOT IN ('Lost','WrittenOff'));
    DECLARE @avgCycleDays DECIMAL(6,2) =
        (SELECT AVG(CAST(cyc AS DECIMAL(10,2))) FROM (
            SELECT DATEDIFF(HOUR, MIN(CustodyUtc), MAX(CustodyUtc)) / 24.0 AS cyc
            FROM ops.TrayCustody
            WHERE CustodyUtc >= DATEADD(DAY, -90, @today)
            GROUP BY TrayId
            HAVING COUNT(*) >= 2) c);
    SET @avgCycleDays = ISNULL(NULLIF(@avgCycleDays,0), 5.0);
    DECLARE @dailyDemand DECIMAL(10,2) =
        (SELECT COUNT(*) * 1.0 / 90 FROM ops.TripLoad tl JOIN ops.Trip t ON t.TripId = tl.TripId
         WHERE t.ActualDepartureUtc >= DATEADD(DAY,-90,@today) AND tl.LoadedUtc IS NOT NULL);
    SET @dailyDemand = ISNULL(@dailyDemand, 0);

    DELETE FROM asset.FleetRecommendation WHERE SnapshotDate = @today;
    INSERT INTO asset.FleetRecommendation
        (SnapshotDate, CirculatingTrays, AvgCycleDays, DailyDemandTrays, TargetUtilization, RecommendedFleet)
    VALUES (@today, @circulating, @avgCycleDays, @dailyDemand, @TargetUtilization,
            CAST(CEILING(@dailyDemand * @avgCycleDays / NULLIF(@TargetUtilization,0)) AS INT));

    PRINT CONCAT('Asset metrics computed for ', CONVERT(varchar(10), @today, 126));
END;
GO
