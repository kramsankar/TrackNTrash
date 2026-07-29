/* =====================================================================================
   Migration 006 — Carry the order lines a planned tray represents on ops.TripLoad.

   Trips were previously held in memory, so this column was never needed. Persisting
   them means the loading scan has to know which shipment lines to advance when a tray
   goes on the vehicle, and that list has to survive a restart with the trip.

   Stored as a comma-separated list rather than a child table: it is written once at
   planning time, read as a whole, and never queried by individual line.
   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('ops.TripLoad', 'OrderLineIds') IS NULL
BEGIN
    ALTER TABLE ops.TripLoad ADD OrderLineIds NVARCHAR(400) NULL;
    PRINT 'Added ops.TripLoad.OrderLineIds';
END
ELSE PRINT 'ops.TripLoad.OrderLineIds already exists';
GO

PRINT 'Migration 006 complete.';
GO
