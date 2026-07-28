# Appendix — D365 Business Central Equivalent

For clients on **Business Central** instead of F&O. The tracking system and Azure integration layer are unchanged; only the ERP-facing adapter differs (`ID365Client` gets a `BcApiClient` implementation, and the outbound trigger source changes).

## Outbound (BC → tracking)

BC has no F&O-style "business events". Use one of:

| Option | Mechanism | Notes |
|--------|-----------|-------|
| **Webhook (recommended)** | BC **API subscriptions** (`POST /subscriptions`) on `salesOrders` / `warehouseShipments` | BC pushes notifications to an Azure Function HTTP endpoint; the function then reads the changed entity via the API and calls tracking `POST /orders`. Near-real-time, no polling. |
| Custom AL event | An AL extension publishes to Service Bus on `OnAfterReleaseSalesDoc` | Most control; requires AL dev. |
| Job queue export | Scheduled AL job posts confirmed orders | Fallback only; not event-driven. |

BC webhook payload is a notification (not the full record); the function re-reads:
```
GET /v2.0/companies({id})/salesOrders({soId})?$expand=salesOrderLines
```

## Inbound (tracking → BC)

Post via **BC standard/custom API pages** (OAuth 2.0, same as F&O adapter shape):

| Tracking event | BC target |
|----------------|-----------|
| `TrayBuildComplete` | `warehousePicks` / register pick (custom API page over `Warehouse Activity`) |
| `ShipmentConfirmed` | `warehouseShipments` → post shipment |
| `ReceivingComplete` | `salesShipments` (posted sales shipment); shortages → BC **quantity adjustment** (`itemJournals`) or a **case**/task via Service/To-do |

## Mapping deltas vs F&O

| Concept | F&O | BC |
|---------|-----|-----|
| Order | `SalesOrderHeadersV2` | `salesOrders` |
| Line correlation | `InventTransId` | `salesOrderLines.systemId` (GUID) |
| Pick confirm | `WHSWorkLine` custom service | `Warehouse Activity Line` register |
| Delivery note | custom `TntDeliveryService` | posted `salesShipments` |
| Shortage adjustment | `InventoryAdjustments` | `itemJournals` |

## What stays identical

- Service Bus topics/queues, `D365PostingService` (idempotency + retry + DLQ), the repair flow, and all mapping *logic* (`Mapping.cs`) — only the concrete `ID365Client` and the outbound trigger source change. Swap `ODataD365Client` for `BcApiClient` in DI and point the outbound Function at the BC webhook.
