# POD, Photo & Signature Storage Design

How receiving captures and stores proof-of-delivery and exception media.

## Blob layout

Azure Blob Storage account with two containers (lifecycle from Module 13):

```
exceptions/                     (retention: 1 year)
  {yyyy}/{MM}/{dd}/
    damage/{trayQr}/{cartonSerial}-{guid}.jpg
    dock/{trayQr}/frame-{guid}.jpg           ← dock camera (Module 4)
pod/                            (retention: per data-retention policy, e.g. 2 years)
  {yyyy}/{MM}/{dd}/
    signature/{trayQr}-{guid}.png
    delivery/{trayQr}-{guid}.jpg
```

## Capture → upload flow

1. **Capture on device** — damage/delivery photos via `MediaPicker.CapturePhotoAsync`; signature via a MAUI `GraphicsView` drawing surface exported to PNG.
2. **Compress** — downscale to ≤ 1600 px long edge, JPEG q≈70 (signatures stay PNG). Keeps uploads small over cellular.
3. **Upload** — the app requests a short-lived **user-delegation SAS** (write-only, single blob, ~10 min) from the API, then PUTs directly to Blob. The device never holds account keys.
4. **Reference** — the returned blob URI is attached to the event:
   - damage → `Exception.PhotoBlobUri`
   - dock frame → `Exception.FrameBlobUri`
   - signature / delivery photo → `ReceivingComplete` meta / a POD record.

## Offline handling

When offline, media is written to the app's local store and queued alongside the scan events. On reconnect the app uploads blobs first, then posts the events referencing the now-valid URIs. Event idempotency keys prevent double-posting.

## Mandatory-photo rule (damage)

The API rejects a `damaged` call without `photoBlobUri` (400). The app enforces ≥1 photo before enabling the "flag damaged" action, so the rule holds even for a thin client.

## Privacy & security

- Signatures/POD may contain personal data → store in `pod/` with restricted access and the org's retention policy; never embed personal data in blob names.
- SAS tokens are write-only, per-blob, short-lived; reads for the ops console (Module 12) use a separate read SAS minted server-side per view.
- Blob public access disabled; access via Entra ID / SAS only.
