import { useState } from "react";
import { api } from "../api";
import type { CurrentUser } from "../auth";

export function Trips({ user }: { user: CurrentUser }) {
  const [vehicleReg, setVehicleReg] = useState("AB12 CDE");
  const [routeCode, setRouteCode] = useState("R-NORTH");
  const [storeCode, setStoreCode] = useState("S-LDN1");
  const [trayQr, setTrayQr] = useState("TRAY-LDN1-000001");
  const [orderLineId, setOrderLineId] = useState<number>(1);
  const [trip, setTrip] = useState<string>("");
  const [scanTray, setScanTray] = useState("TRAY-LDN1-000001");
  const [log, setLog] = useState<string[]>([]);
  const push = (m: string) => setLog((l) => [m, ...l].slice(0, 12));

  async function create() {
    try {
      const r = await api.createTrip({ vehicleReg, routeCode,
        stops: [{ sequence: 1, storeCode }],
        plannedTrays: [{ trayQr, stopSequence: 1, orderLineIds: [orderLineId] }] }, user.getToken());
      setTrip(r.tripNumber); setScanTray(trayQr);
      push(`✅ ${r.tripNumber} created (${r.trays} tray, ${r.stops} stop) · manifest ${r.manifestQr}`);
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  async function load() {
    if (!trip) { push("Create a trip first"); return; }
    try {
      const r = await api.loadTray(trip, scanTray, "driver-console", user.getToken());
      push(`${r.outcome === "WrongTrip" ? "⛔" : "✅"} load ${scanTray}: ${r.outcome}${r.correctTripNumber ? " → " + r.correctTripNumber : ""}${r.tripNowLocked ? " (locked)" : ""}`);
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  async function depart() {
    if (!trip) return;
    try { const r = await api.depart(trip, user.getToken()); push(`🚚 depart: ${JSON.stringify(r)}`); }
    catch (e) { push("❌ " + (e as Error).message); }
  }

  return (
    <div className="view">
      <h2>Trips &amp; Loading</h2>
      <p className="muted">Create a trip with planned trays, load at the dock (wrong-trip is rejected), then depart.</p>

      <div className="card">
        <h3>Create trip</h3>
        <div className="form-grid">
          <label>Vehicle reg<input value={vehicleReg} onChange={(e) => setVehicleReg(e.target.value)} /></label>
          <label>Route<input value={routeCode} onChange={(e) => setRouteCode(e.target.value)} /></label>
          <label>Stop store<input value={storeCode} onChange={(e) => setStoreCode(e.target.value)} /></label>
          <label>Planned tray<input value={trayQr} onChange={(e) => setTrayQr(e.target.value)} /></label>
          <label>Order line id<input type="number" value={orderLineId} onChange={(e) => setOrderLineId(+e.target.value)} /></label>
        </div>
        <button className="primary" onClick={create}>Create trip</button>
        {trip && <span className="pill">trip <b>{trip}</b></span>}
      </div>

      <div className="card">
        <h3>Load &amp; depart</h3>
        <div className="form-grid">
          <label>Scan tray QR<input value={scanTray} onChange={(e) => setScanTray(e.target.value)} /></label>
        </div>
        <div className="btn-row">
          <button disabled={!trip} onClick={load}>Scan tray at dock</button>
          <button disabled={!trip} onClick={depart}>Depart (telemetry)</button>
        </div>
        <p className="muted">Try a tray QR that isn't on this trip to see the wrong-trip rejection.</p>
      </div>

      <div className="card">
        <h3>Activity</h3>
        <ul className="log">{log.map((l, i) => <li key={i}>{l}</li>)}{log.length === 0 && <li className="muted">No activity yet.</li>}</ul>
      </div>
    </div>
  );
}
