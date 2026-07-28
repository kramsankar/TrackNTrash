/* =====================================================================================
   TrackNTrash — Module 11: ETL stored procedures (incremental) operational → mart.
   Hourly: facts (delta by watermark). Nightly: dimension conform + DimDate top-up.
   ===================================================================================== */
SET NOCOUNT ON;
GO

/* ---- Dimension conform (nightly / on demand) ---- */
CREATE OR ALTER PROCEDURE mart.usp_LoadDimensions
AS
BEGIN
    SET NOCOUNT ON;

    MERGE mart.DimStore AS t
    USING (SELECT StoreId, StoreCode, Name, Region FROM ops.Store) AS s
    ON t.StoreId = s.StoreId
    WHEN MATCHED THEN UPDATE SET StoreCode = s.StoreCode, StoreName = s.Name, Region = s.Region
    WHEN NOT MATCHED THEN INSERT (StoreId, StoreCode, StoreName, Region)
        VALUES (s.StoreId, s.StoreCode, s.Name, s.Region);

    MERGE mart.DimVehicle AS t
    USING (SELECT VehicleId, Registration FROM ops.Vehicle) AS s
    ON t.VehicleId = s.VehicleId
    WHEN NOT MATCHED THEN INSERT (VehicleId, Registration) VALUES (s.VehicleId, s.Registration);

    MERGE mart.DimRoute AS t
    USING (SELECT DISTINCT RouteCode FROM ops.Trip WHERE RouteCode IS NOT NULL) AS s
    ON t.RouteCode = s.RouteCode
    WHEN NOT MATCHED THEN INSERT (RouteCode) VALUES (s.RouteCode);

    MERGE mart.DimProduct AS t
    USING (SELECT DISTINCT Gtin, MAX(ProductDescription) AS Descr FROM ops.OrderLine GROUP BY Gtin) AS s
    ON t.Gtin = s.Gtin
    WHEN MATCHED THEN UPDATE SET Description = s.Descr
    WHEN NOT MATCHED THEN INSERT (Gtin, Description) VALUES (s.Gtin, s.Descr);

    MERGE mart.DimCheckpoint AS t
    USING (SELECT CheckpointId, CheckpointCode, Name FROM ref.[Checkpoint]) AS s
    ON t.CheckpointId = s.CheckpointId
    WHEN NOT MATCHED THEN INSERT (CheckpointId, CheckpointCode, CheckpointName)
        VALUES (s.CheckpointId, s.CheckpointCode, s.Name);
END;
GO

/* ---- DimDate top-up (idempotent, fills a rolling window) ---- */
CREATE OR ALTER PROCEDURE mart.usp_LoadDimDate @FromDate DATE = NULL, @ToDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @FromDate = ISNULL(@FromDate, '2025-01-01');
    SET @ToDate   = ISNULL(@ToDate, DATEADD(YEAR, 1, CAST(SYSUTCDATETIME() AS DATE)));

    ;WITH d AS (
        SELECT @FromDate AS dt
        UNION ALL SELECT DATEADD(DAY,1,dt) FROM d WHERE dt < @ToDate
    )
    INSERT INTO mart.DimDate (DateKey,[Date],[Year],[Quarter],[Month],MonthName,[Day],DayOfWeek,DayName,IsWeekend,YearMonth)
    SELECT CONVERT(int, FORMAT(dt,'yyyyMMdd')), dt, YEAR(dt), DATEPART(QUARTER,dt), MONTH(dt),
           DATENAME(MONTH,dt), DAY(dt), DATEPART(WEEKDAY,dt), DATENAME(WEEKDAY,dt),
           CASE WHEN DATEPART(WEEKDAY,dt) IN (1,7) THEN 1 ELSE 0 END, FORMAT(dt,'yyyy-MM')
    FROM d
    WHERE NOT EXISTS (SELECT 1 FROM mart.DimDate x WHERE x.DateKey = CONVERT(int, FORMAT(d.dt,'yyyyMMdd')))
    OPTION (MAXRECURSION 32767);
END;
GO

/* ---- Incremental fact load (hourly) ---- */
CREATE OR ALTER PROCEDURE mart.usp_LoadFactsIncremental
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @wm DATETIME2(3), @now DATETIME2(3) = SYSUTCDATETIME();

    -- FactScanEvent
    SELECT @wm = LastLoadedUtc FROM mart.EtlWatermark WHERE TableName = 'FactScanEvent';
    SET @wm = ISNULL(@wm, '2000-01-01');
    MERGE mart.FactScanEvent AS t
    USING (
        SELECT e.ScanEventId,
               CONVERT(int, FORMAT(e.EventUtc,'yyyyMMdd')) AS DateKey,
               dc.CheckpointKey, ds.StoreKey, dp.ProductKey,
               e.EventType, e.Verdict, e.EventUtc,
               DATEDIFF(MILLISECOND, e.EventUtc, e.IngestedUtc) AS IngestLatencyMs
        FROM ops.ScanEvent e
        LEFT JOIN mart.DimCheckpoint dc ON dc.CheckpointId = e.CheckpointId
        LEFT JOIN ops.OrderLine ol ON ol.OrderLineId = e.OrderLineId
        LEFT JOIN ops.SalesOrder so ON so.SalesOrderId = ol.SalesOrderId
        LEFT JOIN mart.DimStore ds ON ds.StoreId = so.StoreId
        LEFT JOIN mart.DimProduct dp ON dp.Gtin = ol.Gtin
        WHERE e.IngestedUtc > @wm AND e.IngestedUtc <= @now
    ) AS s ON t.ScanEventId = s.ScanEventId
    WHEN NOT MATCHED THEN INSERT (ScanEventId,DateKey,CheckpointKey,StoreKey,ProductKey,EventType,Verdict,IsFirstScanMatch,EventUtc,IngestLatencyMs)
        VALUES (s.ScanEventId,s.DateKey,s.CheckpointKey,s.StoreKey,s.ProductKey,s.EventType,s.Verdict,NULL,s.EventUtc,s.IngestLatencyMs);
    UPDATE mart.EtlWatermark SET LastLoadedUtc = @now WHERE TableName = 'FactScanEvent';
    IF @@ROWCOUNT = 0 INSERT INTO mart.EtlWatermark(TableName,LastLoadedUtc) VALUES ('FactScanEvent',@now);

    -- FactException
    SELECT @wm = LastLoadedUtc FROM mart.EtlWatermark WHERE TableName = 'FactException';
    SET @wm = ISNULL(@wm, '2000-01-01');
    MERGE mart.FactException AS t
    USING (
        SELECT x.ExceptionId, CONVERT(int, FORMAT(x.CreatedUtc,'yyyyMMdd')) AS DateKey,
               dc.CheckpointKey, ds.StoreKey, x.ExceptionType, x.Severity, x.Status,
               CASE WHEN x.ResolvedUtc IS NOT NULL THEN DATEDIFF(MINUTE, x.CreatedUtc, x.ResolvedUtc) END AS ResMin,
               x.CreatedUtc
        FROM ops.Exception x
        LEFT JOIN mart.DimCheckpoint dc ON dc.CheckpointId = x.CheckpointId
        LEFT JOIN mart.DimStore ds ON ds.StoreId = x.StoreId
        WHERE x.CreatedUtc > @wm AND x.CreatedUtc <= @now
    ) AS s ON t.ExceptionId = s.ExceptionId
    WHEN MATCHED THEN UPDATE SET Status = s.Status, ResolutionMinutes = s.ResMin
    WHEN NOT MATCHED THEN INSERT (ExceptionId,DateKey,CheckpointKey,StoreKey,ExceptionType,Severity,Status,ResolutionMinutes,CreatedUtc)
        VALUES (s.ExceptionId,s.DateKey,s.CheckpointKey,s.StoreKey,s.ExceptionType,s.Severity,s.Status,s.ResMin,s.CreatedUtc);
    UPDATE mart.EtlWatermark SET LastLoadedUtc = @now WHERE TableName = 'FactException';
    IF @@ROWCOUNT = 0 INSERT INTO mart.EtlWatermark(TableName,LastLoadedUtc) VALUES ('FactException',@now);

    -- FactShipmentLine: rebuilt for lines whose state changed since watermark
    SELECT @wm = LastLoadedUtc FROM mart.EtlWatermark WHERE TableName = 'FactShipmentLine';
    SET @wm = ISNULL(@wm, '2000-01-01');
    MERGE mart.FactShipmentLine AS t
    USING (
        SELECT ol.OrderLineId,
               CONVERT(int, FORMAT(ISNULL(sls.StateEnteredUtc, so.CreatedUtc),'yyyyMMdd')) AS DateKey,
               ds.StoreKey, dp.ProductKey, ol.ExpectedCartonCount,
               sls.ReceivedCartons, sls.CurrentState,
               CASE WHEN sls.CurrentState = 'Received' AND sls.ReceivedCartons = ol.ExpectedCartonCount THEN 1 ELSE 0 END AS Clean
        FROM ops.OrderLine ol
        JOIN ops.SalesOrder so ON so.SalesOrderId = ol.SalesOrderId
        JOIN mart.DimStore ds ON ds.StoreId = so.StoreId
        LEFT JOIN mart.DimProduct dp ON dp.Gtin = ol.Gtin
        LEFT JOIN ops.ShipmentLineState sls ON sls.OrderLineId = ol.OrderLineId
        WHERE ISNULL(sls.StateEnteredUtc, so.CreatedUtc) > @wm
    ) AS s ON t.OrderLineId = s.OrderLineId
    WHEN MATCHED THEN UPDATE SET ReceivedCartons = s.ReceivedCartons, FinalState = s.CurrentState, IsReceivedClean = s.Clean
    WHEN NOT MATCHED THEN INSERT (OrderLineId,DateKey,StoreKey,ProductKey,ExpectedCartons,ReceivedCartons,FinalState,IsReceivedClean)
        VALUES (s.OrderLineId,s.DateKey,s.StoreKey,s.ProductKey,s.ExpectedCartonCount,ISNULL(s.ReceivedCartons,0),ISNULL(s.CurrentState,'Ordered'),s.Clean);
    UPDATE mart.EtlWatermark SET LastLoadedUtc = @now WHERE TableName = 'FactShipmentLine';
    IF @@ROWCOUNT = 0 INSERT INTO mart.EtlWatermark(TableName,LastLoadedUtc) VALUES ('FactShipmentLine',@now);

    PRINT 'Incremental fact load complete.';
END;
GO
