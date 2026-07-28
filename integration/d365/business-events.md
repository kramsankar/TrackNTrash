# Business Event & Message Contracts

## Outbound from F&O — `SalesOrderConfirmed`

Emitted by an F&O business event when a sales order is confirmed and warehouse work released. Delivered to Service Bus queue `fno-business-events`.

```json
{
  "eventId": "BE-2026-0007731",
  "salesOrderNumber": "SO-001045",
  "customerAccount": "US-004",
  "storeCode": "S-101",
  "orderDate": "2026-07-28T00:00:00Z",
  "requestedShipDate": "2026-07-29T00:00:00Z",
  "lines": [
    {
      "lineNumber": 1,
      "itemId": "ITEM-88",
      "gtin": "09501234567891",
      "quantity": 240,
      "uom": "EA",
      "expectedCartonCount": 10,
      "inventTransId": "0123456789"
    }
  ]
}
```

`eventId` is the **idempotency key** — the F&O `BusinessEventId`.

## Inbound to F&O — `TrackingOutboundEvent`

Published by the tracking API to the Service Bus topic `tracking-events`. The `d365-delivery` subscription filters `type = 'ReceivingComplete'`.

```json
{
  "eventId": "SE-000998877",
  "kind": "ReceivingComplete",
  "orderNumber": "SO-001045",
  "erpReference": "SO-001045",
  "inventTransId": "0123456789",
  "tripNumber": "TRIP-000123",
  "trayQr": "TRAY-LDN1-000042",
  "storeCode": "S-101",
  "lines": [
    { "lineNumber": 1, "gtin": "09501234567891", "expectedQty": 240, "receivedQty": 216 }
  ]
}
```

`kind` ∈ `TrayBuildComplete | ShipmentConfirmed | ReceivingComplete`.
`eventId` is the tracking `ScanEventId` (idempotency key for F&O posting).

## Subscription filters (Service Bus)

| Subscription | Filter |
|--------------|--------|
| `d365-delivery` | `type = 'ReceivingComplete'` |
| `d365-picking` | `type = 'TrayBuildComplete'` |
| `d365-shipment` | `type = 'ShipmentConfirmed'` |

## Repair (dead-letter) message

```json
{ "channel": "d365-inbound", "eventId": "SE-000998877",
  "error": "429 Too Many Requests", "payload": "SO-001045", "deadLetteredUtc": "..." }
```
Consumed by the Power Automate repair flow → notify + one-click re-enqueue to `tracking-events`.
