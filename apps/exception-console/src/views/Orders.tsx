import { useCallback, useEffect, useState } from "react";
import { api, type OrderRow } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";

const STATE_COLORS: Record<string, string> = {
  Ordered: "slate", Picked: "blue", Staged: "indigo", Loaded: "amber",
  InTransit: "violet", Received: "green",
  ShortShipped: "red", Damaged: "red", WrongStore: "red", Lost: "red",
};

const columns = [
  text<OrderRow>("orderNumber", "Order", { pinned: "left" }),
  text<OrderRow>("orderLineId", "Line id"),
  category<OrderRow>("storeCode", "Store"),
  text<OrderRow>("gtin", "GTIN"),
  text<OrderRow>("expectedCartonCount", "Expected"),
  text<OrderRow>("receivedCartons", "Received"),
  badge<OrderRow>("currentState", "State", STATE_COLORS),
  text<OrderRow>("erpReference", "ERP ref"),
];

/** Persistent orders list (EnterpriseGrid) + create/walk panel. */
export function Orders({ user }: { user: CurrentUser }) {
  const [rows, setRows] = useState<OrderRow[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(() => {
    setLoading(true);
    api.listOrders(user.getToken()).then(setRows).catch(() => setRows([])).finally(() => setLoading(false));
  }, [user]);

  useEffect(() => { refresh(); }, [refresh]);

  return (
    <div className="view">
      <div className="view-head">
        <div>
          <h2>Orders</h2>
          <p className="muted">All order lines and their live state, straight from Azure SQL. Persists across refreshes.</p>
        </div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      <div className="grid-wrap">
        {loading && rows.length === 0
          ? <div className="empty">Loading orders…</div>
          : rows.length === 0
            ? <div className="empty">No orders yet — create one below.</div>
            : <EnterpriseGrid<OrderRow>
                columns={columns} rows={rows} getRowId="orderLineId"
                height={360} selection="multiple" stateKey="tnt-orders" exportFileName="orders" />}
      </div>

      <CreatePanel user={user} onCreated={refresh} />
    </div>
  );
}

function CreatePanel({ user, onCreated }: { user: CurrentUser; onCreated: () => void }) {
  const [orderNumber, setOrderNumber] = useState("SO-" + Math.floor(1000 + Math.random() * 8999));
  const [storeCode, setStoreCode] = useState("S-LDN1");
  const [gtin, setGtin] = useState("09501234567891");
  const [expCtn, setExpCtn] = useState(10);
  const [lineId, setLineId] = useState<number | null>(null);
  const [log, setLog] = useState<string[]>([]);
  const push = (m: string) => setLog((l) => [m, ...l].slice(0, 10));

  async function createOrder() {
    try {
      const r = await api.createOrder({ orderNumber, storeCode, erpReference: "D365-" + orderNumber,
        lines: [{ lineNumber: 1, gtin, orderedQty: 240, uom: "EA", expectedCartonCount: expCtn }] }, user.getToken());
      setLineId(r.orderLineIds[0]);
      push(`✅ ${r.orderNumber} created — line ${r.orderLineIds[0]}`);
      onCreated();
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  async function scan(eventType: string, checkpoint: string, verdict?: string) {
    if (!lineId) { push("Create an order first"); return; }
    try {
      const r = await api.scan({ clientEventId: `${orderNumber}-${eventType}-${Date.now()}`, deviceId: "admin-console",
        eventType, checkpoint, orderLineId: lineId, verdict }, user.getToken());
      push(`${r.transitionLegal ? "➡️" : "⛔"} ${eventType}: ${r.newState ?? "—"} ${r.exceptions.length ? "· " + r.exceptions.map((x) => x.type).join(",") : ""}`);
      onCreated();
    } catch (e) { push("❌ " + (e as Error).message); }
  }

  return (
    <div className="card">
      <h3>Create order &amp; walk the checkpoints</h3>
      <div className="form-grid">
        <label>Order number<input value={orderNumber} onChange={(e) => setOrderNumber(e.target.value)} /></label>
        <label>Store code<input value={storeCode} onChange={(e) => setStoreCode(e.target.value)} /></label>
        <label>GTIN<input value={gtin} onChange={(e) => setGtin(e.target.value)} /></label>
        <label>Expected cartons<input type="number" value={expCtn} onChange={(e) => setExpCtn(+e.target.value)} /></label>
      </div>
      <div className="btn-row">
        <button className="primary" onClick={createOrder}>Create order</button>
        {lineId && <span className="pill">line {lineId}</span>}
      </div>
      <div className="btn-row" style={{ marginTop: 8 }}>
        <button disabled={!lineId} onClick={() => scan("TrayBuildComplete", "PickTrayBuild")}>1 · Pick</button>
        <button disabled={!lineId} onClick={() => scan("DockVerification", "DispatchDock", "PASS")}>2 · Dock PASS</button>
        <button disabled={!lineId} onClick={() => scan("TripLoadScan", "VehicleLoad")}>3 · Load</button>
        <button disabled={!lineId} onClick={() => scan("TelemetryDepart", "VehicleLoad")}>4 · Depart</button>
        <button disabled={!lineId} onClick={() => scan("ReceivingComplete", "StoreReceive")}>5 · Receive</button>
        <button className="danger" disabled={!lineId} onClick={() => scan("DockVerification", "DispatchDock", "COUNT_MISMATCH")}>Force mismatch</button>
      </div>
      <ul className="log">{log.map((l, i) => <li key={i}>{l}</li>)}{log.length === 0 && <li className="muted">No activity yet.</li>}</ul>
    </div>
  );
}
