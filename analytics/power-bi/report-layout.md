# Report Layout — Page by Page

Four pages. Brand-neutral, colour-blind-safe palette; every visual has a title and a plain-language subtitle.

## Page 1 — Executive Summary

| Zone | Visual | Field / measure |
|------|--------|-----------------|
| KPI row | 5 cards | `Dispatch Accuracy %`, `OTIF %`, `Open Exceptions`, `Dock Pass Rate %`, `Tray Loss %` |
| Trend | Line chart | `Dispatch Accuracy %` & `OTIF %` by `DimDate[YearMonth]` |
| Mix | Stacked column | Exception Count by `DimCheckpoint[CheckpointName]` over months |
| Slicers | Date range, Region | `DimDate`, `DimStore[Region]` |

KPI cards show MoM delta via `Dispatch Accuracy % MoM` with conditional colour (green ≥ target, red below).

## Page 2 — Live Operations (today)

| Zone | Visual | Notes |
|------|--------|-------|
| Header | Cards | Trips today, Lines in transit, Open criticals |
| Table | Open exceptions with **aging** | `FactException` filtered to open; conditional format on age; sortable by severity |
| Map/list | Trips in progress | by route/vehicle |
| Filter | `DimDate[Date] = TODAY()` (page-level) |

Auto page-refresh 5 min (DirectQuery) so ops sees near-real-time.

## Page 3 — Store Scorecard

| Zone | Visual |
|------|--------|
| Matrix | Store × [OTIF %, Dispatch Accuracy %, Over rate, Short rate, Tray Loss %] |
| Ranking | Bar: worst 10 stores by `Tray Loss %` |
| Detail | Drill-through to a single store's shipment lines & exceptions |

RLS: a store manager opening this page sees only their store (via the `StoreManager` role).

## Page 4 — Asset Health

| Zone | Visual |
|------|--------|
| Cards | Circulating trays, Recommended fleet, Avg turns/month, Tray Loss % |
| Trend | `Tray Turns per Month` by month |
| Bar | `Tray Loss %` by `DimRoute[RouteCode]` and by store |
| Table | Suspected-lost trays with last-seen age (from `asset.*` / FactException) |

## Cross-cutting

- Consistent slicers synced across pages (Date, Region).
- Tooltips: custom report-page tooltips showing the underlying trend for any KPI.
- Accessibility: tab order set, alt text on visuals, no colour-only encoding (add icons/labels).
