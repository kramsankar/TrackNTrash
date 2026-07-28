# DAX Measures

All measures live in a `_Measures` table. Copy into the model (or use the TMDL in `model/`).

## Core reconciliation

```dax
Lines Shipped =
CALCULATE ( COUNTROWS ( FactShipmentLine ), FactShipmentLine[FinalState] IN { "Loaded","InTransit","Received","ShortShipped","Damaged","WrongStore","Lost" } )

Lines Received Clean =
CALCULATE ( COUNTROWS ( FactShipmentLine ), FactShipmentLine[IsReceivedClean] = TRUE() )

Dispatch Accuracy % =
DIVIDE ( [Lines Received Clean], [Lines Shipped] )
```

```dax
First-Scan Match Rate =
VAR firstScans = CALCULATE ( COUNTROWS ( FactScanEvent ), FactScanEvent[EventType] = "CartonScan" )
VAR matched    = CALCULATE ( COUNTROWS ( FactScanEvent ), FactScanEvent[IsFirstScanMatch] = TRUE() )
RETURN DIVIDE ( matched, firstScans )
```

## Exceptions

```dax
Exception Count = COUNTROWS ( FactException )

Open Exceptions =
CALCULATE ( [Exception Count], FactException[Status] IN { "Open","Acknowledged","Escalated" } )

Exception Rate by Checkpoint =
DIVIDE (
    [Exception Count],
    CALCULATE ( COUNTROWS ( FactScanEvent ), ALLEXCEPT ( DimCheckpoint, DimCheckpoint[CheckpointCode] ) )
)

Avg Resolution Hours =
DIVIDE ( AVERAGE ( FactException[ResolutionMinutes] ), 60 )
```

## Dock

```dax
Dock Verifications = CALCULATE ( COUNTROWS ( FactScanEvent ), FactScanEvent[EventType] = "DockVerification" )

Dock Pass Rate % =
DIVIDE (
    CALCULATE ( [Dock Verifications], FactScanEvent[Verdict] = "PASS" ),
    [Dock Verifications]
)

Avg Dock Verification Time (ms) =
CALCULATE ( AVERAGE ( FactScanEvent[IngestLatencyMs] ), FactScanEvent[EventType] = "DockVerification" )
```

## Assets

```dax
Tray Trips = COUNTROWS ( FactTrayTrip )

Trays Not Returned = CALCULATE ( [Tray Trips], FactTrayTrip[Returned] = FALSE() )

Tray Loss % = DIVIDE ( [Trays Not Returned], [Tray Trips] )

Tray Turns per Month =
DIVIDE (
    [Tray Trips],
    DISTINCTCOUNT ( FactTrayTrip[TrayId] ) * DISTINCTCOUNT ( DimDate[YearMonth] )
)

Avg Dwell Hours = AVERAGE ( FactTrayTrip[DwellHours] )
```

## Service level

```dax
OTIF % =   -- On Time In Full
VAR inFull = CALCULATE ( COUNTROWS ( FactShipmentLine ), FactShipmentLine[IsReceivedClean] = TRUE() )
VAR onTimeInFull = CALCULATE ( COUNTROWS ( FactShipmentLine ), FactShipmentLine[IsReceivedClean] = TRUE(), FactShipmentLine[OnTime] = TRUE() )
RETURN DIVIDE ( onTimeInFull, [Lines Shipped] )

OTIF % by Store = [OTIF %]   -- sliced by DimStore on the page
```

## Trend helpers

```dax
Dispatch Accuracy % MoM =
VAR curr = [Dispatch Accuracy %]
VAR prev = CALCULATE ( [Dispatch Accuracy %], DATEADD ( DimDate[Date], -1, MONTH ) )
RETURN curr - prev
```
