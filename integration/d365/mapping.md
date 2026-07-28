# Field Mapping — Tracking System ⇄ D365 F&O

## Outbound: F&O `SalesOrderConfirmed` → tracking order intake

| F&O field | Tracking field (Module 1) | Notes |
|-----------|---------------------------|-------|
| `salesOrderNumber` | `SalesOrder.OrderNumber` | Natural key |
| `salesOrderNumber` | `SalesOrder.ErpReference` | Back-reference |
| `storeCode` (or `customerAccount`) | `SalesOrder.StoreId` (via `Store.StoreCode`) | Store code preferred; falls back to customer account |
| `orderDate` | `SalesOrder.OrderDate` | |
| `requestedShipDate` | `SalesOrder.RequestedDeliveryDate` | |
| `lines[].lineNumber` | `OrderLine.LineNumber` | |
| `lines[].gtin` | `OrderLine.Gtin` | GTIN-14 |
| `lines[].quantity` | `OrderLine.OrderedQty` | |
| `lines[].uom` | `OrderLine.Uom` | |
| `lines[].expectedCartonCount` | `OrderLine.ExpectedCartonCount` | Drives dock/receiving reconciliation |
| `lines[].inventTransId` | `OrderLine.ErpLineReference` | Ties to `WHSWorkLine` for pick confirm |

## Inbound: tracking events → F&O

### `TrayBuildComplete` → picking confirmation
| Tracking | F&O target |
|----------|-----------|
| `orderNumber` | `SalesOrderNumber` |
| `inventTransId` | `WHSWorkLine.InventTransId` (custom service `confirmPick`) |
| `lines[].receivedQty` (picked) | `QtyPicked` |

### `ShipmentConfirmed` (Loaded + departure) → packing slip / ASN
| Tracking | F&O target |
|----------|-----------|
| `orderNumber` | `CustPackingSlipJour.SalesId` |
| `tripNumber` | shipment reference |
| departure time | `DeliveryDate` / ship date |

### `ReceivingComplete` → delivery note
| Tracking | F&O target |
|----------|-----------|
| `orderNumber` | `SalesId` |
| `lines[].receivedQty` | `DeliveryLine.DeliveredQty` |
| `expectedQty − receivedQty` | `DeliveryLine.ShortQty` |
| shortage present | → **case** (`Cases`) *or* **quantity adjustment** (`InventoryAdjustments`) per `PostingOptions.ShortageHandling` |

## Key & idempotency mapping

| Purpose | Tracking | F&O |
|---------|----------|-----|
| Outbound idempotency | — | `BusinessEventId` (`eventId`) |
| Inbound idempotency | `ScanEventId` (`eventId`) | dedupe before post |
| Order correlation | `SalesOrder.ErpReference` | `SalesOrderNumber` |
| Line correlation | `OrderLine.ErpLineReference` | `InventTransId` |
