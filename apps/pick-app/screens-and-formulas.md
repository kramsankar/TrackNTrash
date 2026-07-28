# Screens & Power Fx — Pick App

Phone layout. Four screens: **Pick List → Order → Tray Build → Complete**. All formulas are Power Fx.

## Global (App.OnStart)

```powerapps
// Device + user identity for event attribution / idempotency
Set(gDeviceId, "PICK-" & Text(Rand()*100000, "00000"));   // replace with MDM device id in prod
Set(gUser, User().Email);

// Barcode reader options (QR)
Set(gScanType, BarcodeType.QRCode);

// Working state
Set(gOrder, Blank());
Set(gTray, Blank());
ClearCollect(colScanQueue, []);          // offline scan queue (see offline-and-conflicts.md)
ClearCollect(colScannedSerials, []);     // serials scanned this tray (dedupe)
Set(gOnline, Connection.Connected);
```

---

## Screen 1 — Pick List (`scrPickList`)

Shows sales orders assigned to the current picker (Dataverse), plus a "Scan order" button.

**Gallery `galOrders`.Items**
```powerapps
Filter(tnt_salesorder,
    tnt_assignedto.'Primary Email' = gUser,
    tnt_status.Value in ["Open", "Picking"])
```

**Scan Order button `btnScanOrder`.OnSelect**
```powerapps
Set(gScanResult, BarcodeReader1.Value);   // or use a dedicated scan screen
```

**Gallery item / scan → open order**
```powerapps
// From gallery selection:
Set(gOrder, ThisItem);
Navigate(scrOrder, ScreenTransition.Cover);

// From order QR scan, match on tnt_qrvalue:
With({ o: LookUp(tnt_salesorder, tnt_qrvalue = BarcodeReader1.Value) },
    If(IsBlank(o),
        Notify("Order QR not recognized", NotificationType.Error),
        Set(gOrder, o); Navigate(scrOrder, ScreenTransition.Cover)
    )
)
```

---

## Screen 2 — Order (`scrOrder`)

Lists order lines with picked / remaining, and requires a tray before carton scanning.

**Gallery `galLines`.Items**
```powerapps
Filter(tnt_orderline, tnt_salesorder.tnt_ordernumber = gOrder.tnt_ordernumber)
```

**Per-line remaining label**
```powerapps
ThisItem.tnt_expectedcartoncount - ThisItem.tnt_pickedcount & " remaining"
```

**Scan Tray button → validate + bind**
```powerapps
With({ t: LookUp(tnt_tray, tnt_trayqr = BarcodeReader1.Value) },
    Switch(true,
        IsBlank(t),
            Notify("Unknown tray " & BarcodeReader1.Value, NotificationType.Error),
        t.tnt_status.Value <> "Available",
            Notify("Tray " & t.tnt_trayqr & " is " & t.tnt_status.Value & " — cannot use",
                   NotificationType.Error),
        // else: bind
        true,
            Set(gTray, t);
            Collect(colScanQueue, {
                clientEventId: GUID(),
                eventType: "TrayBind",
                orderNumber: gOrder.tnt_ordernumber,
                trayQr: t.tnt_trayqr,
                scannedQr: t.tnt_trayqr,
                deviceId: gDeviceId, userId: gUser, eventUtc: Now()
            });
            Notify("Tray " & t.tnt_trayqr & " bound", NotificationType.Success);
            Navigate(scrTrayBuild, ScreenTransition.Cover)
    )
)
```

---

## Screen 3 — Tray Build (`scrTrayBuild`)

The scanning workhorse. Each carton scan runs the three validation rules.

**BarcodeReader `brCarton`.OnScan**
```powerapps
// Resolve the scanned carton within THIS order
With(
    {
        c: LookUp(tnt_carton,
                  tnt_qrpayload = brCarton.Value
                  && tnt_orderline.tnt_salesorder.tnt_ordernumber = gOrder.tnt_ordernumber)
    },
    Switch(true,
        // Rule 1 — belongs to this order line
        IsBlank(c),
            Set(gBanner, "❌ Carton not on this order");
            Notify(gBanner, NotificationType.Error),

        // Rule 2 — not already scanned this tray
        !IsBlank(LookUp(colScannedSerials, serial = c.tnt_serial)),
            Set(gBanner, "❌ Already scanned: " & c.tnt_serial);
            Notify(gBanner, NotificationType.Error),

        // Rule 3 — quantity not exceeded for the line
        LookUp(tnt_orderline, tnt_name = c.tnt_orderline.tnt_name).tnt_pickedcount
            >= LookUp(tnt_orderline, tnt_name = c.tnt_orderline.tnt_name).tnt_expectedcartoncount,
            Set(gBanner, "❌ Line already full for " & c.tnt_gtin);
            Notify(gBanner, NotificationType.Error),

        // Success
        true,
            Collect(colScannedSerials, { serial: c.tnt_serial, orderLine: c.tnt_orderline.tnt_name });
            Collect(colScanQueue, {
                clientEventId: GUID(),
                eventType: "CartonScan",
                orderLine: c.tnt_orderline.tnt_name,
                trayQr: gTray.tnt_trayqr,
                scannedQr: brCarton.Value,
                deviceId: gDeviceId, userId: gUser, eventUtc: Now()
            });
            // optimistic local tally
            Patch(tnt_orderline,
                  LookUp(tnt_orderline, tnt_name = c.tnt_orderline.tnt_name),
                  { tnt_pickedcount:
                        LookUp(tnt_orderline, tnt_name = c.tnt_orderline.tnt_name).tnt_pickedcount + 1 });
            Set(gBanner, Blank());
            Notify("✅ " & c.tnt_gtin & "  " & c.tnt_serial, NotificationType.Success);
            // haptic
            Vibrate(50)
    )
)
```

**Progress label**
```powerapps
CountRows(colScannedSerials) & " / " &
Sum(Filter(tnt_orderline, tnt_salesorder.tnt_ordernumber = gOrder.tnt_ordernumber),
    tnt_expectedcartoncount) & " cartons"
```

**Banner (red) `lblBanner`** — Visible: `!IsBlank(gBanner)`, Fill: `RGBA(200,0,0,1)`, Text: `gBanner`.

**Complete button `btnComplete`** — enabled only when all lines full:
```powerapps
// DisplayMode
If(
    CountRows(Filter(tnt_orderline,
        tnt_salesorder.tnt_ordernumber = gOrder.tnt_ordernumber,
        tnt_pickedcount < tnt_expectedcartoncount)) = 0,
    DisplayMode.Edit, DisplayMode.Disabled)
```

**btnComplete.OnSelect → emit TrayBuildComplete + flush queue**
```powerapps
// Append the completion event carrying the full carton manifest
Collect(colScanQueue, {
    clientEventId: GUID(),
    eventType: "TrayBuildComplete",
    orderNumber: gOrder.tnt_ordernumber,
    trayQr: gTray.tnt_trayqr,
    cartons: JSON(colScannedSerials, JSONFormat.Compact),
    deviceId: gDeviceId, userId: gUser, eventUtc: Now()
});

// Fire the Power Automate flow with the queued events (idempotent server-side)
Set(gFlowResult,
    TrayBuildComplete.Run(
        gDeviceId, gUser, gOrder.tnt_ordernumber, gTray.tnt_trayqr,
        JSON(colScanQueue, JSONFormat.IncludeBinaryData)
    )
);

If(gFlowResult.status = "ok",
    // clear local state on success
    Clear(colScanQueue); Clear(colScannedSerials);
    Navigate(scrComplete, ScreenTransition.Cover),
    // keep the queue for retry (offline / failure)
    Notify("Saved locally — will sync when online", NotificationType.Warning);
    Navigate(scrComplete, ScreenTransition.Cover)
)
```

---

## Screen 4 — Complete (`scrComplete`)

Confirmation + "next order". Shows the tray code, carton count, and sync status.

```powerapps
// Sync status label
If(CountRows(colScanQueue) = 0, "✅ Synced", "⏳ " & CountRows(colScanQueue) & " queued")
```

**Next order button** → `Set(gOrder, Blank()); Set(gTray, Blank()); Navigate(scrPickList)`.

---

## Barcode control config

Use the **Barcode reader** control (not the legacy scanner):
- `BarcodeType` = `QRCode` (add `Code128` if cartons ever carry 1-D backup).
- `Scanner` preferred camera = rear.
- `PreferFrontCamera` = false.
- On rugged handhelds with a hardware imager, bind the imager output to the same `OnScan` handler.
