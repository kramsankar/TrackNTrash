# Power BI Data Mart & Dashboard — Module 11

Star-schema analytics over the operational data, with DAX measures, a TMDL semantic model, a four-page report, incremental refresh and store-level RLS.

## Contents

| Path | Role |
|------|------|
| `sql/mart_schema.sql` | Star schema (`mart` schema): 4 facts + 6 dims + ETL watermark |
| `sql/etl.sql` | Incremental ETL stored procs (dims, DimDate, hourly facts) |
| `dax/measures.md` | All DAX measures |
| `model/model.tmdl` | TMDL semantic model (tables, relationships, RLS, key measures) |
| `report-layout.md` | Page-by-page build spec |

## Star schema

```
                DimDate
                   |
DimStore — FactShipmentLine — DimProduct
DimCheckpoint — FactScanEvent — DimStore
DimCheckpoint — FactException — DimStore
DimRoute/DimVehicle/DimStore — FactTrayTrip
```

Facts: `FactScanEvent` (event grain), `FactShipmentLine` (order-line grain), `FactException` (exception grain), `FactTrayTrip` (tray-trip grain). All dimension→fact relationships are single-direction.

## Loads

- **Nightly**: `mart.usp_LoadDimensions`, `mart.usp_LoadDimDate` (rolling window).
- **Hourly**: `mart.usp_LoadFactsIncremental` — delta by `mart.EtlWatermark` (each fact tracks its own high-water mark on `IngestedUtc` / `CreatedUtc` / state-change time). Run via an Azure Function timer, ADF pipeline, or SQL Agent.

## Incremental refresh (Power BI)

Configure on each fact using `DimDate`:
- **Store rows in the last** 3 years; **refresh rows in the last** 10 days.
- Requires `RangeStart` / `RangeEnd` parameters filtering the fact's date column; Power BI partitions by date and only refreshes recent partitions.
- Import mode for facts (fast visuals) + DirectQuery for the Live Operations page if sub-hour freshness is required (composite model).

## RLS — store managers see only their store

`model.tmdl` defines a `StoreManager` role that filters `DimStore` by a `UserStoreMap (Upn → StoreCode)` security table via `USERPRINCIPALNAME()`; the filter propagates through every fact. `Admin` sees all. Map users to stores from Entra group membership or a maintained list. Test with **View as role** before publishing.

## KPIs delivered

Dispatch Accuracy %, First-Scan Match Rate, Exception Rate by checkpoint, Avg Dock Verification time, Tray Loss %, Tray Turns/Month, OTIF by store — see `dax/measures.md`.
