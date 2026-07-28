# Geofence / Telematics Webhook Contract

The telematics provider (or the MAUI app's own background geofence) calls the tracking API when a vehicle crosses a store/warehouse boundary.

## Endpoint

```
POST /events/telemetry
Content-Type: application/json
x-api-key: <telematics shared secret>      # validated at the gateway / APIM
```

## Payload

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tripNumber` | string | ✅ | e.g. `TRIP-000123` |
| `event` | string | ✅ | `depart` \| `arrive` |
| `stopSequence` | int | for `arrive` | Which stop was reached |
| `lat` / `lon` | number | optional | Geofence crossing point |
| `deviceId` | string | optional | Telematics unit / phone id |
| `occurredUtc` | string (ISO-8601) | optional | Defaults to server time |

```json
{ "tripNumber": "TRIP-000123", "event": "depart", "lat": 51.5072, "lon": -0.1276,
  "deviceId": "TELE-77", "occurredUtc": "2026-07-28T08:15:00Z" }
```

## Behavior

| Event | Effect |
|-------|--------|
| `depart` | Trip → `Departed`; every loaded order line transitions **Loaded → InTransit** (via the state machine). Returns `{ tripNumber, transitioned: "InTransit" }`. If the trip is not loadable/departable → `409 Conflict`. |
| `arrive` | Recorded; store receiving (Module 8) drives the `Received` transition. Returns `{ tripNumber, recorded: "arrive" }`. |

## Idempotency & security

- Each transition emits scan events keyed `{tripNumber}:{orderLineId}:depart`, so a duplicate geofence fire is a **no-op** (idempotent on the event log).
- Authenticate the webhook with a shared secret / mTLS at API Management; never trust `tripNumber` alone.
- Reject payloads whose `tripNumber` does not exist (returns 409), so a spoofed call cannot advance arbitrary state.
