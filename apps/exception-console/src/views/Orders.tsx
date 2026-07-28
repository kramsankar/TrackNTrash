import { useState } from "react";
import { api } from "../api";
import type { CurrentUser } from "../auth";

/** Create an order (setup), then pick/dock/load/receive its line through the checkpoints. */
export function Orders({ user }: { user: CurrentUser }) {
  const [orderNumber, setOrderNumber] = useState("SO-1001");
  const [storeCode, setStoreCode] = useState("S-LDN1");
  const [gtin, setGtin] = useState("09501234567891");
  const [expCtn, setExpCtn] = useState(10);
  const [lineIds, setLineIds] = useState<number[]>([]);
  const [state, setState] = useState<string>("");
  const [log, setLog] = useState<string[]>([]);
  const push = (m: string) => setLog((l) => [m, ...l].slice(0, 12));

  async function createOrder() {
    try {
      const r = await api.createOrder({ orderNumber, storeCode, erpReference: "D365-" + orderNumber,
        lines: [{ lineNumber: 1, gtin, orderedQty: 240, uom: "EA", expectedCartonCount: expCtn }] }, user.getToken());
      setLineIds(r.orderLineIds);
      push(`✅ Order ${r.orderNumber} created — line id(s) ${r.orderLineIds.join(", ")}`);
      refreshState(r.orderLineIds[0]);
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  async function refreshState(id: number) {
    try { const s = await api.lineState(id, user.getToken()); setState(s.currentState); } catch { /* ignore */ }
  }

  async function scan(eventType: string, checkpoint: string, verdict?: string) {
    if (!lineIds.length) { push("Create an order first"); return; }
    const id = lineIds[0];
    try {
      const r = await api.scan({ clientEventId: `${orderNumber}-${eventType}-${Date.now()}`, deviceId: "admin-console",
        eventType, checkpoint, orderLineId: id, verdict }, user.getToken());
      push(`${r.transitionLegal ? "➡️" : "⛔"} ${eventType}: state=${r.newState ?? "—"} ${r.exceptions.length ? "exception: " + r.exceptions.map((x) => x.type).join(",") : ""}`);
      await refreshState(id);
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  return (
    <div className="view">
      <h2>Orders &amp; Reconciliation</h2>
      <p className="muted">Create an order (master data), then walk its line through the checkpoints. Persists to Azure SQL.</p>

      <div className="card">
        <h3>Create order</h3>
        <div className="form-grid">
          <label>Order number<input value={orderNumber} onChange={(e) => setOrderNumber(e.target.value)} /></label>
          <label>Store code<input value={storeCode} onChange={(e) => setStoreCode(e.target.value)} /></label>
          <label>GTIN<input value={gtin} onChange={(e) => setGtin(e.target.value)} /></label>
          <label>Expected cartons<input type="number" value={expCtn} onChange={(e) => setExpCtn(+e.target.value)} /></label>
        </div>
        <button className="primary" onClick={createOrder}>Create order</button>
        {lineIds.length > 0 && <span className="pill">line id {lineIds[0]} · state <b>{state || "Ordered"}</b></span>}
      </div>

      <div className="card">
        <h3>Move line through checkpoints</h3>
        <div className="btn-row">
          <button disabled={!lineIds.length} onClick={() => scan("TrayBuildComplete", "PickTrayBuild")}>1 · Pick / Tray build</button>
          <button disabled={!lineIds.length} onClick={() => scan("DockVerification", "DispatchDock", "PASS")}>2 · Dock PASS</button>
          <button disabled={!lineIds.length} onClick={() => scan("TripLoadScan", "VehicleLoad")}>3 · Load</button>
          <button disabled={!lineIds.length} onClick={() => scan("TelemetryDepart", "VehicleLoad")}>4 · Depart</button>
          <button disabled={!lineIds.length} onClick={() => scan("ReceivingComplete", "StoreReceive")}>5 · Receive</button>
        </div>
        <div className="btn-row">
          <button className="danger" disabled={!lineIds.length} onClick={() => scan("DockVerification", "DispatchDock", "COUNT_MISMATCH")}>Force dock COUNT_MISMATCH</button>
          <button className="danger" disabled={!lineIds.length} onClick={() => scan("ReceivingComplete", "StoreReceive")}>Illegal (out-of-order receive)</button>
        </div>
      </div>

      <div className="card">
        <h3>Activity</h3>
        <ul className="log">{log.map((l, i) => <li key={i}>{l}</li>)}{log.length === 0 && <li className="muted">No activity yet.</li>}</ul>
      </div>
    </div>
  );
}
