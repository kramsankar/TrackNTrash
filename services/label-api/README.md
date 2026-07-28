# Label API — Module 2

.NET 8 minimal API that generates **GS1-compliant serialized QR labels** for cartons, SSCCs and trays.

## Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/labels/carton` | Serialized carton labels: `(01)GTIN (21)serial`, PNG + optional ZPL |
| POST | `/labels/sscc` | SSCC-18 with company prefix + DB serial reference + check digit |
| POST | `/labels/tray` | Internal asset QR `TRAY-{siteCode}-{seq}`, optional laser-etch SVG |
| GET | `/health` | Liveness |

Swagger UI at `/swagger` in Development.

### `/labels/carton`
```json
POST { "gtin": "09501234567891", "orderLineReference": "SO1001-1", "quantity": 3, "includeZpl": true }
→ [ { "serial": "000000000001",
      "gtin": "09501234567891",
      "gs1ElementString": "(01)09501234567891(21)000000000001",
      "qrPayload": "010950123456789121000000000001",
      "pngBase64": "iVBORw0K…",
      "zpl": "^XA…^XZ" }, … ]
```

### `/labels/sscc`
```json
POST { "quantity": 2, "includeZpl": false }
→ [ { "sscc": "006141410000000012",
      "gs1ElementString": "(00)006141410000000012",
      "qrPayload": "00006141410000000012",
      "pngBase64": "…" }, … ]
```
Company prefix and extension digit come from config (`Label:Sscc`). Serial reference is drawn from the DB sequence `ref.SsccSerialReference` (or an in-memory counter in dev).

### `/labels/tray`
```json
POST { "siteCode": "LDN1", "quantity": 1, "laserEtchSvg": true }
→ [ { "trayQr": "TRAY-LDN1-000001", "pngBase64": "…", "svg": "<svg …>" } ]
```

## GS1 & QR policy

- **Check digits** — GTIN-14 and SSCC-18 use GS1 mod-10 (`Gs1CheckDigit`). Carton GTINs are validated on input; a bad check digit returns `400`.
- **Element string** vs **QR payload** — element string is human-readable `(01)…(21)…`; the QR payload is the FNC1-mode data string (`Gs1Encoding`).
- **Error correction** — cartons **Q** (~25%), trays **H** (~30%, "2x") for harsh environments / laser etch.
- **ZPL** — 4×6 in @ 203 dpi, `^BQ` GS1-QR (`QA,` prefix) + human-readable text. Tunable via `Label:Zpl`.

## Serial providers

`Label:SerialProvider` = `InMemory` (default; dev/test) or `Sql`. The SQL provider calls
`NEXT VALUE FOR` on the Module 1 sequences (`ref.SsccSerialReference`, `ref.CartonSerialReference`, `ref.TraySequence`) and needs `Label:ConnectionString`.

## Run

```bash
dotnet run --project LabelApi
```
Then open http://localhost:5080/swagger.

## Test

```bash
dotnet test
```
18 tests: GTIN-14 / SSCC-18 check digits (incl. known vectors and bad-digit detection) and label generation (GS1 format, uniqueness, PNG/SVG/ZPL, validation).

## Project layout

```
label-api/
├── LabelApi/
│   ├── Program.cs                 minimal API + DI
│   ├── Gs1/Gs1CheckDigit.cs       mod-10 check digit
│   ├── Gs1/Gs1Encoding.cs         element strings + QR payloads
│   ├── Models/LabelModels.cs      request/response records
│   ├── Options/LabelOptions.cs    config
│   └── Services/
│       ├── ISerialNumberProvider.cs / InMemory… / SqlSerialNumberProvider.cs
│       ├── QrImageService.cs      QRCoder PNG/SVG
│       ├── ZplRenderer.cs         Zebra ZPL
│       └── LabelService.cs        orchestration
└── LabelApi.Tests/                xUnit
```
