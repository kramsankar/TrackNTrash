/* =====================================================================================
   Migration 004 — Widen the carton serial charset to match GS1.

   The original CK_Carton_Serial allowed only [0-9A-Za-z], but the GS1 General
   Specifications define AI (21) Serial over "character set 82", which includes
   - . / _ and other punctuation. Real-world serials routinely contain hyphens
   (e.g. CTN-VIS-001), so the stricter rule rejected valid input.

   This relaxes the constraint to the practical subset: alphanumerics plus - . / _
   still capped at 20 characters. Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('ops.CK_Carton_Serial', 'C') IS NOT NULL
BEGIN
    ALTER TABLE ops.Carton DROP CONSTRAINT CK_Carton_Serial;
    PRINT 'Dropped old CK_Carton_Serial';
END
GO

IF OBJECT_ID('ops.CK_Carton_Serial', 'C') IS NULL
BEGIN
    ALTER TABLE ops.Carton ADD CONSTRAINT CK_Carton_Serial
        CHECK (Serial NOT LIKE '%[^0-9A-Za-z._/-]%' AND LEN(Serial) BETWEEN 1 AND 20);
    PRINT 'Added widened CK_Carton_Serial (GS1 charset subset)';
END
GO

PRINT 'Migration 004 complete.';
GO
