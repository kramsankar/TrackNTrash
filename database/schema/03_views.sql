/* =====================================================================================
   TrackNTrash — Module 1: Convenience views for the API / reporting layers
   Run AFTER 01_schema.sql.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- views persist this setting; required with filtered-index dependencies
GO

/* Current tray contents (active bindings only). */
CREATE OR ALTER VIEW ops.vTrayContentsActive AS
SELECT tc.TrayId, t.TrayQr, tc.CartonId, c.Gtin, c.Serial, c.Status AS CartonStatus, tc.BoundUtc
FROM ops.TrayContent tc
JOIN ops.Tray   t ON t.TrayId  = tc.TrayId
JOIN ops.Carton c ON c.CartonId = tc.CartonId
WHERE tc.UnboundUtc IS NULL;
GO

/* Order line reconciliation: expected vs picked vs received carton counts. */
CREATE OR ALTER VIEW ops.vOrderLineReconciliation AS
SELECT
    ol.OrderLineId,
    so.OrderNumber,
    s.StoreCode,
    ol.Gtin,
    ol.ExpectedCartonCount,
    COUNT(CASE WHEN c.Status IN ('Picked','Staged','Loaded','Received') THEN 1 END) AS PickedOrLater,
    COUNT(CASE WHEN c.Status = 'Received' THEN 1 END) AS ReceivedCartons,
    sls.CurrentState
FROM ops.OrderLine ol
JOIN ops.SalesOrder so ON so.SalesOrderId = ol.SalesOrderId
JOIN ops.Store       s ON s.StoreId       = so.StoreId
LEFT JOIN ops.Carton c ON c.OrderLineId   = ol.OrderLineId
LEFT JOIN ops.ShipmentLineState sls ON sls.OrderLineId = ol.OrderLineId
GROUP BY ol.OrderLineId, so.OrderNumber, s.StoreCode, ol.Gtin, ol.ExpectedCartonCount, sls.CurrentState;
GO

/* Current tray custodian (latest custody record per tray). */
CREATE OR ALTER VIEW ops.vTrayCurrentCustody AS
SELECT tc.TrayId, t.TrayQr, tc.ToCustodianType AS CustodianType, tc.ToCustodianRef AS CustodianRef,
       tc.CustodyUtc, tc.TripId
FROM ops.TrayCustody tc
JOIN ops.Tray t ON t.TrayId = tc.TrayId
WHERE tc.TrayCustodyId = (
    SELECT MAX(tc2.TrayCustodyId) FROM ops.TrayCustody tc2 WHERE tc2.TrayId = tc.TrayId
);
GO

/* Open exceptions enriched for the ops console. */
CREATE OR ALTER VIEW ops.vOpenExceptions AS
SELECT e.ExceptionId, e.ExceptionType, e.Severity, e.Status,
       cp.CheckpointCode, e.OrderLineId, e.TrayId, e.TripId, e.StoreId,
       e.Detail, e.FrameBlobUri, e.PhotoBlobUri, e.CreatedUtc,
       DATEDIFF(MINUTE, e.CreatedUtc, SYSUTCDATETIME()) AS AgeMinutes
FROM ops.Exception e
LEFT JOIN ref.[Checkpoint] cp ON cp.CheckpointId = e.CheckpointId
WHERE e.Status IN ('Open','Acknowledged','Escalated');
GO

PRINT N'TrackNTrash views created.';
GO
