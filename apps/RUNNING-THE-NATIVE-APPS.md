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

## Sign in first

The tracking API does not accept scans anonymously. Every app opens on a **0 · SIGN IN**
card; everything below it stays greyed out until you are signed in.

Use your TrackNTrash username and password — the same ones the exception console uses.
The token is written to the device's preferences and reapplied on the next launch, so a
warehouse handset only asks once. **Sign out** clears it.

If a call ever comes back `401`, the token has expired — sign out and back in.

## A full run-through (all three together)

0. **Sign in** on each app.
1. **Pick app** — click **Start order** (creates an order in Azure SQL, note the **order line id**), type a couple of carton codes pressing Enter after each, then **Complete tray build**. The line becomes **Picked**.
2. **Driver app** — put that **order line id** in, **Create trip** (note the trip number and tray), **Load tray** (try a different tray QR to see the **wrong-trip** rejection), then **Depart**. The line becomes **Loaded → In Transit**.
3. **Receiving app** — set the tray + store + expected carton codes, **Set up & start receiving**, scan the codes (scan an unexpected one to see **OVER → correct store**), then **Complete delivery**. The line becomes **Received**.

Watch it all land in the **Admin Console** (Exceptions / Line Lookup) and in **Azure SQL** — same events, same database.

## Phone targets — camera QR scanning

Each app already targets **Android** (and iOS, on a Mac) and includes **camera QR scanning** via `ZXing.Net.MAUI`. Every screen has a **📷 Scan with camera** button that opens a live camera scanner; the decoded QR feeds the exact same logic as the keyboard field, so desktop and phone share one code path. Camera permissions are declared (Android `CAMERA`, iOS `NSCameraUsageDescription`).

The app `.csproj` targets:
```xml
<TargetFrameworks>net9.0-android;net9.0-windows10.0.19041.0</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">$(TargetFrameworks);net9.0-ios</TargetFrameworks>
```

### Build for Android (produces an APK)

One-time setup — the `maui-android` workload plus the Android SDK:
```bash
dotnet workload install maui-android
# acquire the Android SDK + accept licenses (needed once):
dotnet build apps/pick-app/maui -t:InstallAndroidDependencies -f net9.0-android \
  -p:AndroidSdkDirectory="C:/Android/sdk" -p:AcceptAndroidSdkLicenses=True
```
Then build / deploy:
```bash
dotnet build apps/pick-app/maui -f net9.0-android -c Release      # → .apk under bin/…/net9.0-android/
dotnet build apps/pick-app/maui -t:Run -f net9.0-android          # deploy to a connected device/emulator
```

### Release-signed APKs (what is published on the download page)

`AndroidSdkDirectory` is needed here too — the SDK lives at `C:/Android/sdk`, not the
path the Android targets probe by default, so a build without it fails `XA5300`.

```bash
dotnet publish apps/pick-app/maui -c Release -f net9.0-android \
  -p:AndroidSdkDirectory="C:/Android/sdk" \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=".secrets/tracktrash-release.keystore" \
  -p:AndroidSigningKeyAlias=tracktrash \
  -p:AndroidSigningKeyPass="$KEYSTORE_PASSWORD" \
  -p:AndroidSigningStorePass="$KEYSTORE_PASSWORD"
```

The keystore password must not contain `%` — MSBuild treats it as an escape and signing
fails with a misleading error.
On a phone the **📷 Scan** button uses the real camera. iOS builds require a Mac + `dotnet workload install maui-ios`.

## Point at a different API

Edit `Services/TrackApiClient.cs` in each app — change `BaseUrl` to your API (defaults to the live Azure deployment).
