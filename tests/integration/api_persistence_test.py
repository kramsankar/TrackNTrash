#!/usr/bin/env python3
"""
End-to-end API + persistence tests for the TrackNTrash tracking API.

The existing xUnit suites exercise the domain against in-memory stores, which is why
they stayed green while several features quietly failed to persist. This suite is the
complement: it drives the deployed HTTP API and then asserts the rows actually reached
Azure SQL, so "the endpoint returned 200" is never mistaken for "the data was saved".

Usage:
    python api_persistence_test.py --api <url> --sql-password <pw> [--skip-db]

Exit code is non-zero if any check fails, so it can gate a deploy.
"""

import argparse
import json
import subprocess
import sys
import urllib.error
import urllib.request
import uuid

# Windows terminals default to cp1252; the report uses box characters.
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

SQLCMD = r"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd"
SQL_SERVER = "sql-tracktrash-dev-r4p4og.database.windows.net"
SQL_DB = "TrackNTrash"
SQL_USER = "tntadmin"

results = []          # (area, name, status, detail)
_token = None


def record(area, name, ok, detail=""):
    results.append((area, name, "PASS" if ok else "FAIL", detail))
    print(f"  [{'PASS' if ok else 'FAIL'}] {name}" + (f" — {detail}" if detail else ""))
    return ok


def call(api, method, path, body=None, token=None, expect=None):
    """Returns (status, parsed_json_or_text)."""
    url = api + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    if data:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=45) as r:
            raw = r.read().decode()
            try:
                return r.status, json.loads(raw)
            except json.JSONDecodeError:
                return r.status, raw
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except json.JSONDecodeError:
            return e.code, raw
    except Exception as e:                                    # network / timeout
        return 0, str(e)


def sql(password, query):
    """Runs a scalar query and returns the trimmed first line, or None."""
    try:
        out = subprocess.run(
            [SQLCMD, "-S", SQL_SERVER, "-d", SQL_DB, "-U", SQL_USER, "-P", password,
             "-N", "-C", "-I", "-h", "-1", "-W", "-Q", "SET NOCOUNT ON; " + query],
            capture_output=True, text=True, timeout=60)
        lines = [l.strip() for l in out.stdout.splitlines() if l.strip()]
        return lines[0] if lines else None
    except Exception:
        return None


def count(password, table, where="1=1"):
    v = sql(password, f"SELECT COUNT(*) FROM {table} WHERE {where};")
    try:
        return int(v)
    except (TypeError, ValueError):
        return None


# ─────────────────────────────────────────────────────────────── test areas ──

def test_health(api):
    print("\n── Health & auth ──")
    s, b = call(api, "GET", "/health")
    record("health", "GET /health returns ok", s == 200 and isinstance(b, dict) and b.get("status") == "ok", f"HTTP {s}")

    s, b = call(api, "GET", "/auth/config")
    record("health", "GET /auth/config advertises a sign-in method",
           s == 200 and (b.get("local") or b.get("entra")), f"local={b.get('local') if isinstance(b, dict) else '?'}")


def test_auth(api, admin_user, admin_pw):
    global _token
    print("\n── Authentication ──")
    s, b = call(api, "POST", "/auth/login", {"username": admin_user, "password": admin_pw})
    ok = s == 200 and isinstance(b, dict) and b.get("token")
    record("auth", "Correct credentials issue a token", ok, f"HTTP {s}")
    if ok:
        _token = b["token"]

    s, _ = call(api, "POST", "/auth/login", {"username": admin_user, "password": "definitely-wrong"})
    record("auth", "Wrong password is rejected with 401", s == 401, f"HTTP {s}")

    s, _ = call(api, "GET", "/console/exceptions")
    record("auth", "Protected endpoint refuses anonymous access", s == 401, f"HTTP {s}")

    s, _ = call(api, "GET", "/console/exceptions", token=_token)
    record("auth", "Protected endpoint accepts a valid token", s == 200, f"HTTP {s}")

    # The operational endpoints were reachable anonymously long after auth was switched
    # on: anyone could read the order book or post scan events.
    for method, path, body in [
        ("GET", "/orders", None),
        ("GET", "/assets", None),
        ("GET", "/exceptions/open", None),
        ("GET", "/manifests?since=2000-01-01T00:00:00Z", None),
        ("POST", "/events/scan", {"clientEventId": "anon-" + uuid.uuid4().hex[:8],
                                  "deviceId": "anon", "eventType": "TrayBuildComplete", "orderLineId": 1}),
        ("POST", "/trips", {"vehicleReg": "ANON"}),
        ("PUT", "/asn", {"trayQr": "ANON", "storeCode": "ANON", "expectedCartons": []}),
    ]:
        s, _ = call(api, method, path, body)
        record("auth", f"Anonymous {method} {path.split('?')[0]} is refused", s == 401, f"HTTP {s}")

    # /health is the probe the platform calls; it must stay open.
    s, _ = call(api, "GET", "/health")
    record("auth", "Health check stays open", s == 200, f"HTTP {s}")

    # Enumerating by hand missed the Items and Cameras groups entirely, because those
    # endpoints close with "}).WithTags(...)" rather than a line starting with .WithTags.
    # This sweeps every readable endpoint instead of trusting a list.
    OPEN_BY_DESIGN = {"/health", "/auth/config", "/auth/login", "/auth/users"}
    leaked = []
    for path in ["/orders", "/assets", "/assets/summary", "/cartons", "/items/counts",
                 "/cameras", "/sitemaps", "/console/exceptions", "/exceptions/open",
                 "/masters", "/masters/product", "/rbac/forms", "/rbac/users",
                 "/rbac/mappings", "/manifests?since=2000-01-01T00:00:00Z"]:
        if path in OPEN_BY_DESIGN:
            continue
        s, _ = call(api, "GET", path)
        if s != 401:
            leaked.append(f"{path}={s}")
    record("auth", "No GET endpoint is readable anonymously",
           not leaked, ", ".join(leaked) if leaked else "all 401")


def test_orders(api, pw, skip_db):
    print("\n── Orders (create → walk checkpoints → persist) ──")
    order = "SO-IT-" + uuid.uuid4().hex[:6].upper()
    s, b = call(api, "POST", "/orders", {
        "orderNumber": order, "storeCode": "S-LDN1", "erpReference": "IT-" + order,
        "lines": [{"lineNumber": 1, "gtin": "09501234567891", "orderedQty": 240,
                   "uom": "EA", "expectedCartonCount": 10}]}, token=_token)
    ok = s == 200 and b.get("orderLineIds")
    record("orders", "Create order", ok, f"HTTP {s}")
    if not ok:
        return None
    line_id = b["orderLineIds"][0]

    s, b = call(api, "GET", "/orders", token=_token)
    record("orders", "Order appears in the list (survives a fresh read)",
           s == 200 and any(r.get("orderNumber") == order for r in b), f"{len(b) if isinstance(b, list) else '?'} rows")

    if not skip_db:
        record("orders", "Order row reached SQL", count(pw, "ops.SalesOrder", f"OrderNumber='{order}'") == 1)
        record("orders", "Order line row reached SQL", count(pw, "ops.OrderLine", f"OrderLineId={line_id}") == 1)

    # Walk the happy path and assert each transition.
    steps = [("TrayBuildComplete", "PickTrayBuild", None, "Picked"),
             ("DockVerification", "DispatchDock", "PASS", "Staged"),
             ("TripLoadScan", "VehicleLoad", None, "Loaded"),
             ("TelemetryDepart", "VehicleLoad", None, "InTransit"),
             ("ReceivingComplete", "StoreReceive", None, "Received")]
    for event, cp, verdict, expected in steps:
        body = {"clientEventId": f"{order}-{event}", "deviceId": "integration-test",
                "eventType": event, "checkpoint": cp, "orderLineId": line_id}
        if verdict:
            body["verdict"] = verdict
        s, b = call(api, "POST", "/events/scan", body, token=_token)
        record("orders", f"{event} → {expected}",
               s == 200 and b.get("newState") == expected, f"got {b.get('newState') if isinstance(b, dict) else b}")

    s, b = call(api, "GET", f"/shipment-lines/{line_id}/state", token=_token)
    record("orders", "Final state is Received", s == 200 and b.get("currentState") == "Received",
           str(b.get("currentState") if isinstance(b, dict) else b))

    if not skip_db:
        record("orders", "Scan events reached SQL",
               (count(pw, "ops.ScanEvent", f"OrderLineId={line_id}") or 0) >= 5)
        record("orders", "State projection reached SQL",
               sql(pw, f"SELECT CurrentState FROM ops.ShipmentLineState WHERE OrderLineId={line_id};") == "Received")
        record("orders", "Transition history reached SQL",
               (count(pw, "ops.ShipmentLineStateHistory", f"OrderLineId={line_id}") or 0) >= 5)
    return line_id


def test_idempotency(api, line_id):
    print("\n── Idempotency ──")
    body = {"clientEventId": "idem-" + uuid.uuid4().hex[:8], "deviceId": "integration-test",
            "eventType": "TrayBuildComplete", "orderLineId": line_id}
    s1, b1 = call(api, "POST", "/events/scan", body, token=_token)
    s2, b2 = call(api, "POST", "/events/scan", body, token=_token)
    record("events", "Replayed event is detected as duplicate",
           s2 == 200 and b2.get("duplicate") is True, f"first dup={b1.get('duplicate')}, second dup={b2.get('duplicate')}")
    record("events", "Duplicate returns the original event id",
           b1.get("scanEventId") == b2.get("scanEventId"))


def test_illegal_transition(api, pw, skip_db):
    print("\n── Illegal transition raises an exception ──")
    order = "SO-ILL-" + uuid.uuid4().hex[:6].upper()
    s, b = call(api, "POST", "/orders", {"orderNumber": order, "storeCode": "S-LDN1",
                "lines": [{"lineNumber": 1, "gtin": "09501234567891", "orderedQty": 1,
                           "expectedCartonCount": 1}]}, token=_token)
    if s != 200:
        record("events", "Setup order for illegal transition", False, f"HTTP {s}")
        return
    line_id = b["orderLineIds"][0]
    s, b = call(api, "POST", "/events/scan", {
        "clientEventId": f"{order}-illegal", "deviceId": "integration-test",
        "eventType": "ReceivingComplete", "checkpoint": "StoreReceive", "orderLineId": line_id}, token=_token)
    record("events", "Out-of-order receive is refused as a transition",
           s == 200 and b.get("transitionLegal") is False, f"legal={b.get('transitionLegal')}")
    record("events", "…but the event is still recorded (append-only)", s == 200 and b.get("accepted") is True)
    record("events", "…and an IllegalTransition exception is raised",
           any(e.get("type") == "IllegalTransition" for e in (b.get("exceptions") or [])))
    if not skip_db:
        record("events", "State did NOT advance in SQL",
               sql(pw, f"SELECT CurrentState FROM ops.ShipmentLineState WHERE OrderLineId={line_id};") == "Ordered")


def test_assets(api, pw, skip_db):
    print("\n── Assets (trays) ──")
    s, b = call(api, "POST", "/assets/register", {"siteCode": "ITST", "count": 2}, token=_token)
    ok = s == 200 and b.get("registered") == 2
    record("assets", "Register trays", ok, f"HTTP {s}")
    if ok and not skip_db:
        qr = b["trayQrs"][0]
        record("assets", "Tray reached SQL", count(pw, "ops.Tray", f"TrayQr='{qr}'") == 1)
    s, b = call(api, "GET", "/assets/summary", token=_token)
    record("assets", "Summary reports totals", s == 200 and isinstance(b, dict) and b.get("total", 0) > 0)


def test_items(api, pw, skip_db):
    print("\n── Item-level counting ──")
    serial = "CTN-IT-" + uuid.uuid4().hex[:5].upper()
    s, b = call(api, "POST", "/cartons", {"orderLineId": 1, "gtin": "09501234567891",
                "serial": serial, "expectedItemCount": 10, "itemIdentification": "Visual"}, token=_token)
    ok = s == 200 and b.get("cartonId")
    record("items", "Create carton", ok, f"HTTP {s}")
    if not ok:
        return
    cid = b["cartonId"]

    s, b = call(api, "POST", "/items/count", {"cartonId": cid, "checkpoint": "DispatchDock",
                "scannedBarcodes": [], "visionCount": 10}, token=_token)
    record("items", "Vision count matching expectation → MATCH", s == 200 and b.get("verdict") == "MATCH", str(b.get("verdict")))

    s, b = call(api, "POST", "/items/count", {"cartonId": cid, "checkpoint": "StoreReceive",
                "scannedBarcodes": [], "visionCount": 7}, token=_token)
    record("items", "Vision count below expectation → SHORT", s == 200 and b.get("verdict") == "SHORT", str(b.get("verdict")))

    if not skip_db:
        record("items", "Counts reached SQL", (count(pw, "ops.ItemCount", f"CartonId={cid}") or 0) >= 2)
        record("items", "SHORT raised an exception row in SQL",
               (count(pw, "ops.Exception", f"CartonId={cid}") or 0) >= 1)


def test_trips(api, pw, skip_db):
    print("\n── Trips (the persistence gap) ──")
    s, b = call(api, "POST", "/trips", {"vehicleReg": "IT-TRUCK", "routeCode": "R-IT",
                "stops": [{"sequence": 1, "storeCode": "S-LDN1"}],
                "plannedTrays": [{"trayQr": "TRAY-LDN1-000001", "stopSequence": 1, "orderLineIds": [1]}]}, token=_token)
    ok = s == 200 and b.get("tripNumber")
    record("trips", "Create trip returns a trip number", ok, f"HTTP {s}")
    if not ok:
        return
    trip = b["tripNumber"]

    s, b = call(api, "GET", f"/trips/{trip}", token=_token)
    record("trips", "Trip readable from the API", s == 200 and b.get("tripNumber") == trip)

    if not skip_db:
        record("trips", "Trip PERSISTED to SQL", count(pw, "ops.Trip", f"TripNumber='{trip}'") == 1,
               "trips are held in memory only — lost on restart")


def test_masters(api, pw, skip_db):
    print("\n── Master data CRUD ──")
    s, b = call(api, "GET", "/masters", token=_token)
    record("masters", "Master registry lists types", s == 200 and isinstance(b, list) and len(b) >= 7)

    code = "IT" + uuid.uuid4().hex[:4].upper()
    s, b = call(api, "POST", "/masters/store", {"storeCode": code, "name": "Integration Test Store",
                "city": "London", "isActive": True}, token=_token)
    ok = s == 200 and b.get("id")
    record("masters", "Create store", ok, f"HTTP {s}")
    if not ok:
        return
    sid = b["id"]

    s, b = call(api, "PUT", f"/masters/store/{sid}", {"name": "Renamed Store"}, token=_token)
    record("masters", "Update store", s == 200, f"HTTP {s}")
    if not skip_db:
        record("masters", "Update reached SQL",
               sql(pw, f"SELECT Name FROM ops.Store WHERE StoreId={sid};") == "Renamed Store")

    s, _ = call(api, "DELETE", f"/masters/store/{sid}", token=_token)
    record("masters", "Delete (soft) store", s == 200, f"HTTP {s}")
    if not skip_db:
        record("masters", "Soft delete set IsActive=0 (row retained)",
               count(pw, "ops.Store", f"StoreId={sid} AND IsActive=0") == 1)

    s, b = call(api, "POST", "/masters/store", {"storeCode": code, "name": "Duplicate"}, token=_token)
    record("masters", "Duplicate code is rejected with a clear error",
           s == 400 and "exists" in json.dumps(b).lower(), f"HTTP {s}")

    s, b = call(api, "GET", "/masters/not-a-real-master", token=_token)
    record("masters", "Unknown master type is rejected", s == 400, f"HTTP {s}")


def test_rbac(api):
    print("\n── RBAC ──")
    s, b = call(api, "GET", "/rbac/forms", token=_token)
    record("rbac", "Forms registry populated", s == 200 and len(b) >= 18, f"{len(b) if isinstance(b, list) else '?'} forms")

    s, b = call(api, "GET", "/rbac/permissions?username=admin", token=_token)
    admin_full = isinstance(b, list) and len(b) >= 18 and all(x.get("canView") for x in b)
    record("rbac", "Admin resolves to every form with full rights", admin_full)

    # The endpoint derives the subject from the token, so a ?username= for someone else
    # must NOT return their permissions — that would be an information-disclosure hole.
    s, b = call(api, "GET", "/rbac/permissions?username=dispatcher", token=_token)
    same_as_admin = isinstance(b, list) and len(b) >= 18
    record("rbac", "?username= cannot be used to read another user's permissions", same_as_admin,
           f"returned the caller's own {len(b) if isinstance(b, list) else '?'} forms")

    # Dispatcher's restricted set is verified through the mapping table instead.
    s, b = call(api, "GET", "/rbac/mappings", token=_token)
    disp = [m for m in b if m.get("roleName") == "Dispatcher"] if isinstance(b, list) else []
    record("rbac", "Dispatcher role has a restricted mapping set", 0 < len(disp) < 18,
           f"{len(disp)} forms mapped")

    s, b = call(api, "GET", "/rbac/users", token=_token)
    record("rbac", "Users list returns rows", s == 200 and isinstance(b, list) and len(b) >= 1)


def test_cameras(api, pw, skip_db):
    print("\n── Cameras & site map ──")
    code = "CAM-IT-" + uuid.uuid4().hex[:4].upper()
    s, b = call(api, "POST", "/cameras", {"cameraCode": code, "name": "IT camera", "cameraKind": "Fixed",
                "siteCode": "ITST", "zone": "Test", "purpose": "ItemCount", "status": "Active"}, token=_token)
    ok = s == 200 and b.get("cameraId")
    record("cameras", "Register camera", ok, f"HTTP {s}")
    if ok and not skip_db:
        record("cameras", "Camera reached SQL", count(pw, "ops.Camera", f"CameraCode='{code}'") == 1)
    if ok:
        s, _ = call(api, "POST", f"/cameras/{b['cameraId']}/placement",
                    {"siteMapId": 1, "x": 0.5, "y": 0.5}, token=_token)
        record("cameras", "Place camera on the site map", s == 200, f"HTTP {s}")
        if not skip_db:
            record("cameras", "Placement reached SQL",
                   count(pw, "ops.CameraPlacement", f"CameraId={b['cameraId']}") == 1)


def test_receiving(api, pw, skip_db):
    print("\n── Store receiving ──")
    tray = "TRAY-IT-" + uuid.uuid4().hex[:4].upper()
    s, _ = call(api, "PUT", "/asn", {"trayQr": tray, "storeCode": "S-LDN1",
                "expectedCartons": [{"payload": "P1", "orderLineId": 1},
                                    {"payload": "P2", "orderLineId": 1}]}, token=_token)
    record("receiving", "Seed ASN", s == 200, f"HTTP {s}")

    s, b = call(api, "POST", "/receiving/start", {"trayQr": tray, "storeCode": "S-LDN1"}, token=_token)
    ok = s == 200 and b.get("sessionId")
    record("receiving", "Start receiving session", ok, f"HTTP {s}")
    if not ok:
        return
    sess = b["sessionId"]

    if not skip_db:
        # Sessions used to be a dictionary: a recycle mid-tray made the colleague at the
        # door start again from the first carton, and the id counter rewound with it.
        record("receiving", "Session row reached SQL",
               count(pw, "ops.ReceivingSession", f"SessionId='{sess}'") == 1)

    s, b = call(api, "POST", f"/receiving/{sess}/scan", {"payload": "P1"}, token=_token)
    record("receiving", "Expected carton → Received", s == 200 and b.get("outcome") == "Received", str(b.get("outcome")))

    if not skip_db:
        # The scan has to be written back, or the next request rehydrates an empty session.
        record("receiving", "Scan PERSISTED to the session",
               count(pw, "ops.ReceivingSessionScan",
                     f"Payload='P1' AND ReceivingSessionId="
                     f"(SELECT ReceivingSessionId FROM ops.ReceivingSession WHERE SessionId='{sess}')") == 1)

    s, b = call(api, "POST", f"/receiving/{sess}/scan", {"payload": "STRANGER"}, token=_token)
    record("receiving", "Unexpected carton → Over", s == 200 and b.get("outcome") == "Over", str(b.get("outcome")))

    s, b = call(api, "POST", f"/receiving/{sess}/complete",
                {"deviceId": "integration-test", "receiverName": "IT Runner"}, token=_token)
    record("receiving", "Complete reports the short carton",
           s == 200 and "P2" in (b.get("shortPayloads") or []), str(b.get("shortPayloads")))

    if not skip_db:
        rows = count(pw, "ops.TrayCustody") or 0
        record("receiving", "Tray custody PERSISTED to SQL", rows > 0, f"{rows} custody rows")
        # ASNs used to live in memory: a recycle left an inbound tray with no expected
        # list, so every carton read as an over-scan.
        record("receiving", "ASN header reached SQL",
               count(pw, "ops.Asn", f"TrayQr='{tray}'") == 1)
        record("receiving", "ASN lines reached SQL",
               count(pw, "ops.AsnLine",
                     f"AsnId=(SELECT AsnId FROM ops.Asn WHERE TrayQr='{tray}')") == 2)


def test_console_exceptions(api, pw, skip_db):
    print("\n── Exception console ──")
    # The console read a private in-memory list, so a restart showed an empty board
    # while ops.Exception still held every unactioned row.
    s, b = call(api, "GET", "/console/exceptions", token=_token)
    listed = b if isinstance(b, list) else []
    record("console", "Console lists exceptions", s == 200 and len(listed) > 0, f"{len(listed)} listed")
    if not listed:
        return

    if not skip_db:
        in_sql = count(pw, "ops.Exception") or 0
        record("console", "Console list matches the SQL row count",
               len(listed) == in_sql, f"console={len(listed)} sql={in_sql}")

    # Ids must be real ExceptionIds, not a local counter — otherwise actions 404.
    target = listed[0]["id"]
    s, b = call(api, "POST", f"/console/exceptions/{target}/acknowledge",
                {"user": "integration-test", "note": "audit check"}, token=_token)
    record("console", "Acknowledge succeeds against a listed id", s == 200, f"HTTP {s}")

    s, b = call(api, "GET", f"/console/exceptions/{target}", token=_token)
    ex = (b or {}).get("exception") or {}
    record("console", "Status is Acknowledged on read-back", ex.get("status") == "Acknowledged", str(ex.get("status")))
    record("console", "Audit trail records the actor",
           any(a.get("user") == "integration-test" for a in (ex.get("audit") or [])),
           f"{len(ex.get('audit') or [])} audit entries")

    if not skip_db:
        record("console", "Action PERSISTED to SQL",
               count(pw, "ops.ExceptionAudit",
                     f"ExceptionId={target} AND ActionedByUser='integration-test'") >= 1)


def test_manifests(api, pw, skip_db):
    print("\n── Manifests ──")
    tray = "TRAY-MF-" + uuid.uuid4().hex[:4].upper()
    s, _ = call(api, "PUT", "/manifests", {"trayQr": tray, "expectedCartonCount": 5,
                "expectedCartonPayloads": ["A", "B"]}, token=_token)
    record("manifests", "Upsert manifest", s == 200, f"HTTP {s}")
    s, b = call(api, "GET", "/manifests?since=2000-01-01T00:00:00Z", token=_token)
    record("manifests", "Delta sync returns the manifest",
           s == 200 and any(m.get("trayQr") == tray for m in (b.get("manifests") or [])))
    if not skip_db:
        record("manifests", "Manifest reached SQL", count(pw, "ops.TrayManifest", f"TrayQr='{tray}'") == 1)


def test_validation(api):
    print("\n── Input validation ──")
    for name, path, body, expect in [
        ("Scan without clientEventId is rejected", "/events/scan", {"eventType": "TrayBuildComplete"}, 400),
        ("Order without storeCode is rejected", "/orders", {"orderNumber": "X"}, 400),
        ("Camera without code is rejected", "/cameras", {"name": "x"}, 400),
        ("Asset register with count 0 is rejected", "/assets/register", {"siteCode": "X", "count": 0}, 400),
    ]:
        s, _ = call(api, "POST", path, body, token=_token)
        record("validation", name, s == expect, f"HTTP {s}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="https://app-tracking-tracktrash-dev-4ymqn2.azurewebsites.net")
    ap.add_argument("--user", default="admin")
    ap.add_argument("--password", required=True, help="console admin password")
    ap.add_argument("--sql-password", default=None, help="SQL admin password; omit to skip DB assertions")
    args = ap.parse_args()
    skip_db = not args.sql_password
    pw = args.sql_password

    print(f"API: {args.api}\nDB assertions: {'OFF' if skip_db else 'ON'}")

    test_health(args.api)
    test_auth(args.api, args.user, args.password)
    line_id = test_orders(args.api, pw, skip_db)
    if line_id:
        test_idempotency(args.api, line_id)
    test_illegal_transition(args.api, pw, skip_db)
    test_assets(args.api, pw, skip_db)
    test_items(args.api, pw, skip_db)
    test_trips(args.api, pw, skip_db)
    test_masters(args.api, pw, skip_db)
    test_rbac(args.api)
    test_cameras(args.api, pw, skip_db)
    test_receiving(args.api, pw, skip_db)
    test_console_exceptions(args.api, pw, skip_db)
    test_manifests(args.api, pw, skip_db)
    test_validation(args.api)

    passed = sum(1 for *_, st, _ in results if st == "PASS")
    failed = [r for r in results if r[2] == "FAIL"]
    print("\n" + "=" * 66)
    print(f"TOTAL {len(results)}   PASSED {passed}   FAILED {len(failed)}")
    if failed:
        print("\nFailures:")
        for area, name, _, detail in failed:
            print(f"  • [{area}] {name}" + (f" — {detail}" if detail else ""))
    print("=" * 66)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
