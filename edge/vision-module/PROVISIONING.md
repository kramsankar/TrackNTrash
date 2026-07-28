# Edge Device Provisioning & Camera Commissioning

## 1. Edge device

1. Install Azure IoT Edge runtime (1.5 LTS) on the gateway device (x64 or ARM64).
2. Register the device in IoT Hub; provision with its device connection string (or DPS).
3. Grant Docker access to `/dev/gpiomem` (relay) — the deployment sets `Privileged` + device mapping.
4. Log in to ACR and deploy: `az iot edge set-modules --hub-name <hub> --device-id <dev> --content deployment.json`.

## 2. Model

Place the exported `carton_yolov8n.onnx` (from Module 5) at `models/` in the image build, or mount it via a mapped volume for field updates without a rebuild.

## 3. Camera commissioning checklist

| Item | Target |
|------|--------|
| Mount | Overhead, centered on the staging zone, lens parallel to the tray top |
| Height | 2.2–2.8 m above the floor (tune so a full tray fills ~70% of frame) |
| Field of view | Entire tray footprint + 20% margin; avoid neighbouring trays in frame |
| Lighting | ≥ 500 lux even, diffuse; no direct specular glare on carton tape/labels |
| Resolution | ≥ 1080p; fixed focus locked after commissioning |
| Shutter | Fast enough to freeze a tray sliding into the zone (≥ 1/250s) |

### QR size vs distance (rule of thumb)
A QR module must be ≥ 2 px to decode reliably. For a 1080p camera at 2.5 m over a ~1.2 m FOV (≈ 0.55 mm/px), a 25 mm QR (v3, ~29 modules → ~0.86 mm/module ≈ 1.5 px) is marginal — **use ≥ 35 mm carton QR** (≥ 2.2 px/module) or increase resolution. Trays use larger high-ECC codes, so they decode comfortably.

## 4. Verify

- Trigger a test verification (direct method `trigger`) and confirm a `DockVerification` message reaches IoT Hub.
- Place a known-good tray → expect `PASS`; remove a carton → expect `MISSING_CARTON` + an annotated frame in the `exceptions` blob container + relay pulse.

## 5. Drift monitoring

- 1% of `PASS` frames sampled to Blob for monthly re-validation (see Module 5 drift plan).
- Alert if PASS-rate or mean verification time deviates > 2σ from the rolling baseline.
