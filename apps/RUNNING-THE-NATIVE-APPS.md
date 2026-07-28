# Running the native handheld apps (.NET MAUI)

All three handheld apps are now **native .NET MAUI desktop apps** you can run on Windows — no Power Apps, no phone, no emulator. They talk to the **live Azure API** and use **keyboard / USB-scanner entry** (type, paste, or scan with a USB barcode scanner, which acts as a keyboard).

| App | Folder | What it does |
|-----|--------|--------------|
| **Pick** | `apps/pick-app/maui` | Create an order → scan cartons into a tray → complete tray build (→ Picked) |
| **Driver** | `apps/driver-app/maui` | Create a trip → scan trays to load (wrong-tray rejected) → depart (→ In Transit) |
| **Receiving** | `apps/receiving-app/maui` | Set up a delivery (ASN) → scan cartons (over/short) → complete with POD (→ Received) |

## Prerequisites (one time)

```bash
dotnet workload install maui-windows
```
(.NET 9 SDK required — already present on this machine.)

## Run an app

Easiest — from the repo root:

```bash
dotnet run --project apps/pick-app/maui -f net9.0-windows10.0.19041.0
dotnet run --project apps/driver-app/maui -f net9.0-windows10.0.19041.0
dotnet run --project apps/receiving-app/maui -f net9.0-windows10.0.19041.0
```

Or double-click the built executable, e.g.:

```
apps/pick-app/maui/bin/Debug/net9.0-windows10.0.19041.0/win10-x64/TrackNTrash.PickApp.exe
```

Each app shows **● Connected** at the top when it reaches the live API.

## A full run-through (all three together)

1. **Pick app** — click **Start order** (creates an order in Azure SQL, note the **order line id**), type a couple of carton codes pressing Enter after each, then **Complete tray build**. The line becomes **Picked**.
2. **Driver app** — put that **order line id** in, **Create trip** (note the trip number and tray), **Load tray** (try a different tray QR to see the **wrong-trip** rejection), then **Depart**. The line becomes **Loaded → In Transit**.
3. **Receiving app** — set the tray + store + expected carton codes, **Set up & start receiving**, scan the codes (scan an unexpected one to see **OVER → correct store**), then **Complete delivery**. The line becomes **Received**.

Watch it all land in the **Admin Console** (Exceptions / Line Lookup) and in **Azure SQL** — same events, same database.

## Target a phone instead of the desktop

These are built Windows-only for quick running. To target real handhelds, add the mobile TFMs back to the app's `.csproj` and install the full workload:

```xml
<TargetFrameworks>net9.0-windows10.0.19041.0;net9.0-android;net9.0-ios</TargetFrameworks>
```
```bash
dotnet workload install maui
```
On phones you'd also wire camera scanning (e.g. `ZXing.Net.MAUI`) in place of the keyboard entry field.

## Point at a different API

Edit `Services/TrackApiClient.cs` in each app — change `BaseUrl` to your API (defaults to the live Azure deployment).
