/* =====================================================================================
   TrackNTrash — Module 1: Reference / lookup seed data
   Run AFTER 01_schema.sql. Idempotent (MERGE-based).
   ===================================================================================== */

SET NOCOUNT ON;
GO

/* ---- Checkpoints ------------------------------------------------------------------ */
MERGE ref.[Checkpoint] AS t
USING (VALUES
    (1, 'PickTrayBuild', N'Pick & Tray Build', 1),
    (2, 'DispatchDock',  N'Dispatch Dock',     2),
    (3, 'VehicleLoad',   N'Vehicle Loading',   3),
    (4, 'StoreReceive',  N'Store Receiving',   4)
) AS s(CheckpointId, CheckpointCode, Name, SortOrder)
ON t.CheckpointId = s.CheckpointId
WHEN MATCHED THEN UPDATE SET CheckpointCode = s.CheckpointCode, Name = s.Name, SortOrder = s.SortOrder
WHEN NOT MATCHED THEN INSERT (CheckpointId, CheckpointCode, Name, SortOrder)
    VALUES (s.CheckpointId, s.CheckpointCode, s.Name, s.SortOrder);
GO

/* ---- Shipment states -------------------------------------------------------------- */
MERGE ref.ShipmentState AS t
USING (VALUES
    ('Ordered',      N'Ordered',       0, 0, 1),
    ('Picked',       N'Picked',        0, 0, 2),
    ('Staged',       N'Staged',        0, 0, 3),
    ('Loaded',       N'Loaded',        0, 0, 4),
    ('InTransit',    N'In Transit',    0, 0, 5),
    ('Received',     N'Received',      1, 0, 6),
    -- terminal exceptions
    ('ShortShipped', N'Short Shipped', 1, 1, 90),
    ('Damaged',      N'Damaged',       1, 1, 91),
    ('WrongStore',   N'Wrong Store',   1, 1, 92),
    ('Lost',         N'Lost',          1, 1, 93)
) AS s(StateCode, Name, IsTerminal, IsException, SortOrder)
ON t.StateCode = s.StateCode
WHEN MATCHED THEN UPDATE SET Name = s.Name, IsTerminal = s.IsTerminal, IsException = s.IsException, SortOrder = s.SortOrder
WHEN NOT MATCHED THEN INSERT (StateCode, Name, IsTerminal, IsException, SortOrder)
    VALUES (s.StateCode, s.Name, s.IsTerminal, s.IsException, s.SortOrder);
GO

/* ---- Event types ------------------------------------------------------------------ */
MERGE ref.EventType AS t
USING (VALUES
    ('TrayBind',            N'Tray bound to order',           1),
    ('CartonScan',          N'Carton scanned into tray',      1),
    ('TrayBuildComplete',   N'Tray build complete',           1),
    ('DockVerification',    N'Dock camera verification',      2),
    ('TripLoadScan',        N'Tray loaded to trip',           3),
    ('TelemetryDepart',     N'Vehicle departed (geofence)',   3),
    ('StoreReceiveScan',    N'Carton scanned at store',       4),
    ('ReceivingComplete',   N'Store receiving complete',      4),
    ('TrayCustodyTransfer', N'Tray custody transfer',         NULL),
    ('EmptyTrayReturn',     N'Empty tray return scan',        NULL)
) AS s(EventTypeCode, Name, CheckpointId)
ON t.EventTypeCode = s.EventTypeCode
WHEN MATCHED THEN UPDATE SET Name = s.Name, CheckpointId = s.CheckpointId
WHEN NOT MATCHED THEN INSERT (EventTypeCode, Name, CheckpointId)
    VALUES (s.EventTypeCode, s.Name, s.CheckpointId);
GO

PRINT N'TrackNTrash reference data seeded.';
GO
