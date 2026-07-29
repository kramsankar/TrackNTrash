import { useCallback, useEffect, useState } from "react";
import { api, type CartonRow, type ItemCountRow, type CameraRow } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";

const VERDICT_COLORS: Record<string, string> = {
  MATCH: "green", SHORT: "red", OVER: "amber", UNVERIFIED: "slate",
};
const IDENT_COLORS: Record<string, string> = {
  Barcoded: "blue", Visual: "violet", Mixed: "amber",
};
const CHECKPOINTS = ["PickTrayBuild", "DispatchDock", "StoreReceive"];

const cartonCols = [
  text<CartonRow>("serial", "Carton serial", { pinned: "left" }),
  text<CartonRow>("cartonId", "Id"),
  text<CartonRow>("gtin", "GTIN"),
  text<CartonRow>("expectedItemCount", "Expected units"),
  text<CartonRow>("registeredItems", "Barcoded units"),
  badge<CartonRow>("itemIdentification", "Identification", IDENT_COLORS),
  category<CartonRow>("status", "Status"),
];

const countCols = [
  text<ItemCountRow>("cartonSerial", "Carton", { pinned: "left" }),
  category<ItemCountRow>("checkpoint", "Checkpoint"),
  text<ItemCountRow>("expectedCount", "Expected"),
  text<ItemCountRow>("scannedCount", "Scanned"),
  text<ItemCountRow>("visionCount", "Vision"),
  badge<ItemCountRow>("verdict", "Verdict", VERDICT_COLORS),
  category<ItemCountRow>("cameraCode", "Camera"),
];

export function Items({ user }: { user: CurrentUser }) {
  const [cartons, setCartons] = useState<CartonRow[]>([]);
  const [counts, setCounts] = useState<ItemCountRow[]>([]);
  const [cameras, setCameras] = useState<CameraRow[]>([]);

  const refresh = useCallback(() => {
    api.listCartons(user.getToken()).then(setCartons).catch(() => setCartons([]));
    api.listItemCounts(user.getToken()).then(setCounts).catch(() => setCounts([]));
    api.listCameras(user.getToken()).then(setCameras).catch(() => setCameras([]));
  }, [user]);
  useEffect(() => { refresh(); }, [refresh]);

  return (
    <div className="view">
      <div className="view-head">
        <div>
          <h2>Item-Level Counting</h2>
          <p className="muted">
            Units inside each carton — scanned when barcoded, counted by camera when not.
            Reconciled at pick, dock and store receiving.
          </p>
        </div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      <SetupPanel user={user} onDone={refresh} />
      <CountPanel user={user} cartons={cartons} cameras={cameras} onDone={refresh} />

      <h3>Cartons</h3>
      <div className="grid-wrap">
        {cartons.length === 0
          ? <div className="empty">No cartons defined yet — set one up above.</div>
          : <EnterpriseGrid<CartonRow> columns={cartonCols} rows={cartons} getRowId="cartonId"
              height={260} stateKey="tnt-cartons" exportFileName="cartons" />}
      </div>

      <h3>Count observations</h3>
      <div className="grid-wrap">
        {counts.length === 0
          ? <div className="empty">No counts recorded yet.</div>
          : <EnterpriseGrid<ItemCountRow> columns={countCols} rows={counts} getRowId="itemCountId"
              height={300} stateKey="tnt-itemcounts" exportFileName="item-counts" />}
      </div>
    </div>
  );
}

function SetupPanel({ user, onDone }: { user: CurrentUser; onDone: () => void }) {
  const [serial, setSerial] = useState("CTN-" + Math.floor(10000 + Math.random() * 89999));
  const [gtin, setGtin] = useState("09501234567891");
  const [expected, setExpected] = useState(24);
  const [ident, setIdent] = useState("Visual");
  const [barcodes, setBarcodes] = useState("");
  const [msg, setMsg] = useState("");

  async function create() {
    try {
      const items = ident === "Visual" ? [] :
        barcodes.split(/[,\n]/).map((b) => b.trim()).filter(Boolean).map((barcode) => ({ barcode }));
      const r = await api.createCarton({ orderLineId: 1, gtin, serial, expectedItemCount: expected,
        itemIdentification: ident, items }, user.getToken());
      setMsg(`✅ Carton ${serial} (id ${r.cartonId}) — ${expected} units, ${ident}${r.itemsRegistered ? `, ${r.itemsRegistered} barcodes` : ""}`);
      onDone();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="card">
      <h3>1 · Define a carton and how its units are identified</h3>
      <div className="form-grid">
        <label>Carton serial<input value={serial} onChange={(e) => setSerial(e.target.value)} /></label>
        <label>GTIN<input value={gtin} onChange={(e) => setGtin(e.target.value)} /></label>
        <label>Expected units<input type="number" value={expected} onChange={(e) => setExpected(+e.target.value)} /></label>
        <label>Identification
          <select value={ident} onChange={(e) => setIdent(e.target.value)}>
            <option value="Visual">Visual — camera counts unlabelled units</option>
            <option value="Barcoded">Barcoded — each unit scanned</option>
            <option value="Mixed">Mixed — some barcoded, some not</option>
          </select>
        </label>
        {ident !== "Visual" && (
          <label className="wide">Unit barcodes (comma or newline separated)
            <input value={barcodes} onChange={(e) => setBarcodes(e.target.value)} placeholder="U-001, U-002, U-003…" />
          </label>
        )}
      </div>
      <div className="btn-row">
        <button className="primary" onClick={create}>Create carton</button>
        {msg && <span className="pill">{msg}</span>}
      </div>
    </div>
  );
}

function CountPanel({ user, cartons, cameras, onDone }:
  { user: CurrentUser; cartons: CartonRow[]; cameras: CameraRow[]; onDone: () => void }) {
  const [cartonId, setCartonId] = useState<number | "">("");
  const [checkpoint, setCheckpoint] = useState(CHECKPOINTS[0]);
  const [scanned, setScanned] = useState("");
  const [vision, setVision] = useState<string>("");
  const [cameraId, setCameraId] = useState<number | "">("");
  const [result, setResult] = useState<string>("");

  const carton = cartons.find((c) => c.cartonId === cartonId);

  async function record() {
    if (!cartonId) { setResult("Pick a carton first"); return; }
    try {
      const r = await api.recordItemCount({
        cartonId: Number(cartonId), checkpoint,
        scannedBarcodes: scanned.split(/[,\n]/).map((b) => b.trim()).filter(Boolean),
        visionCount: vision === "" ? null : Number(vision),
        cameraId: cameraId === "" ? null : Number(cameraId),
        confidence: vision === "" ? undefined : 0.94,
        deviceId: "console",
      }, user.getToken());
      const icon = r.verdict === "MATCH" ? "✅" : r.verdict === "UNVERIFIED" ? "•" : "⛔";
      setResult(`${icon} ${r.verdict} — ${r.detail}`);
      onDone();
    } catch (e) { setResult("❌ " + (e as Error).message); }
  }

  return (
    <div className="card">
      <h3>2 · Count the units at a checkpoint</h3>
      <p className="muted" style={{ marginTop: -4, fontSize: 13 }}>
        Scan barcoded units, enter a camera's visual count, or both — the two are cross-checked.
      </p>
      <div className="form-grid">
        <label>Carton
          <select value={cartonId} onChange={(e) => setCartonId(e.target.value === "" ? "" : +e.target.value)}>
            <option value="">Select…</option>
            {cartons.map((c) => <option key={c.cartonId} value={c.cartonId}>{c.serial} ({c.expectedItemCount} × {c.itemIdentification})</option>)}
          </select>
        </label>
        <label>Checkpoint
          <select value={checkpoint} onChange={(e) => setCheckpoint(e.target.value)}>
            {CHECKPOINTS.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>
        <label>Vision count (camera)
          <input type="number" value={vision} onChange={(e) => setVision(e.target.value)} placeholder="e.g. 24" />
        </label>
        <label>Camera
          <select value={cameraId} onChange={(e) => setCameraId(e.target.value === "" ? "" : +e.target.value)}>
            <option value="">None</option>
            {cameras.map((c) => <option key={c.cameraId} value={c.cameraId}>{c.cameraCode} — {c.zone ?? c.siteCode}</option>)}
          </select>
        </label>
        <label className="wide">Scanned unit barcodes
          <input value={scanned} onChange={(e) => setScanned(e.target.value)} placeholder="U-001, U-002…" />
        </label>
      </div>
      <div className="btn-row">
        <button className="primary" onClick={record}>Record count</button>
        {carton && <span className="pill">expects {carton.expectedItemCount} units</span>}
        {result && <span className="pill">{result}</span>}
      </div>
    </div>
  );
}
