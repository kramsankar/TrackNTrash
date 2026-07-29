import { useCallback, useEffect, useRef, useState } from "react";
import { api, type CameraRow, type SiteMapRow } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";
import "../grid-dark.css";

const STATUS_COLORS: Record<string, string> = {
  Active: "green", Offline: "red", Maintenance: "amber", Retired: "slate",
};
const KIND_COLORS: Record<string, string> = { Fixed: "blue", Handheld: "violet" };

const cols = [
  text<CameraRow>("cameraCode", "Camera", { pinned: "left" }),
  text<CameraRow>("name", "Name"),
  badge<CameraRow>("cameraKind", "Kind", KIND_COLORS),
  category<CameraRow>("siteCode", "Site"),
  category<CameraRow>("zone", "Zone"),
  text<CameraRow>("station", "Station"),
  category<CameraRow>("checkpoint", "Checkpoint"),
  category<CameraRow>("purpose", "Purpose"),
  badge<CameraRow>("status", "Status", STATUS_COLORS),
];

export function Cameras({ user }: { user: CurrentUser }) {
  const [cameras, setCameras] = useState<CameraRow[]>([]);
  const [maps, setMaps] = useState<SiteMapRow[]>([]);
  const [mapId, setMapId] = useState<number | "">("");
  const [msg, setMsg] = useState("");

  const refresh = useCallback(() => {
    api.listCameras(user.getToken()).then(setCameras).catch(() => setCameras([]));
    api.listSiteMaps(user.getToken()).then((m) => {
      setMaps(m);
      setMapId((cur) => (cur === "" && m.length ? m[0].siteMapId : cur));
    }).catch(() => setMaps([]));
  }, [user]);
  useEffect(() => { refresh(); }, [refresh]);

  const map = maps.find((m) => m.siteMapId === mapId) ?? null;

  return (
    <div className="view">
      <div className="view-head">
        <div>
          <h2>Cameras &amp; Site Map</h2>
          <p className="muted">
            Fixed and handheld inspection cameras, their location (site → zone → station),
            and where they sit on the floor plan. Drag a camera on the map to reposition it.
          </p>
        </div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      <SiteMapPanel user={user} maps={maps} mapId={mapId} setMapId={setMapId} onDone={refresh} setMsg={setMsg} />

      {map && (
        <FloorPlan map={map} cameras={cameras.filter((c) => c.cameraKind === "Fixed")}
          user={user} onMoved={refresh} />
      )}

      <RegisterPanel user={user} onDone={refresh} />

      <h3>All cameras</h3>
      <div className="grid-wrap">
        {cameras.length === 0
          ? <div className="empty">No cameras registered yet.</div>
          : <EnterpriseGrid<CameraRow> columns={cols} rows={cameras} getRowId="cameraId"
              height={300} stateKey="tnt-cameras" exportFileName="cameras" />}
      </div>
      {msg && <span className="pill">{msg}</span>}
    </div>
  );
}

/** Interactive floor plan — click empty space to place the selected camera, drag pins to move. */
function FloorPlan({ map, cameras, user, onMoved }:
  { map: SiteMapRow; cameras: CameraRow[]; user: CurrentUser; onMoved: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  const [dragging, setDragging] = useState<number | null>(null);
  const [selected, setSelected] = useState<number | "">("");
  const placed = cameras.filter((c) => c.x != null && c.y != null && c.siteMapId === map.siteMapId);
  const unplaced = cameras.filter((c) => c.x == null || c.siteMapId !== map.siteMapId);

  function toFraction(e: React.MouseEvent) {
    const box = ref.current!.getBoundingClientRect();
    return {
      x: Math.min(1, Math.max(0, (e.clientX - box.left) / box.width)),
      y: Math.min(1, Math.max(0, (e.clientY - box.top) / box.height)),
    };
  }

  async function save(cameraId: number, x: number, y: number) {
    await api.placeCamera(cameraId, { siteMapId: map.siteMapId, x: +x.toFixed(4), y: +y.toFixed(4) }, user.getToken());
    onMoved();
  }

  async function onMapClick(e: React.MouseEvent) {
    if (dragging !== null || selected === "") return;
    const { x, y } = toFraction(e);
    await save(Number(selected), x, y);
    setSelected("");
  }

  async function onMouseUp(e: React.MouseEvent) {
    if (dragging === null) return;
    const { x, y } = toFraction(e);
    const id = dragging;
    setDragging(null);
    await save(id, x, y);
  }

  return (
    <div className="card">
      <h3>Floor plan · {map.name}</h3>
      <div className="btn-row" style={{ marginBottom: 10 }}>
        <label className="inline-label">
          Place camera
          <select value={selected} onChange={(e) => setSelected(e.target.value === "" ? "" : +e.target.value)}>
            <option value="">Select a camera…</option>
            {unplaced.map((c) => <option key={c.cameraId} value={c.cameraId}>{c.cameraCode}</option>)}
            {placed.map((c) => <option key={c.cameraId} value={c.cameraId}>{c.cameraCode} (move)</option>)}
          </select>
        </label>
        <span className="muted" style={{ fontSize: 12 }}>
          {selected !== "" ? "Now click a spot on the plan." : "Or drag a pin to reposition it."}
        </span>
      </div>

      <div ref={ref} className={`floorplan ${selected !== "" ? "placing" : ""}`}
        style={{ aspectRatio: `${map.width} / ${map.height}`, backgroundImage: map.imageUri ? `url(${map.imageUri})` : undefined }}
        onClick={onMapClick} onMouseUp={onMouseUp} onMouseLeave={() => setDragging(null)}>
        {placed.map((c) => (
          <button key={c.cameraId} type="button"
            className={`cam-pin ${c.status !== "Active" ? "down" : ""} ${dragging === c.cameraId ? "dragging" : ""}`}
            style={{ left: `${Number(c.x) * 100}%`, top: `${Number(c.y) * 100}%` }}
            onMouseDown={(e) => { e.stopPropagation(); setDragging(c.cameraId); }}
            onClick={(e) => e.stopPropagation()}
            title={`${c.cameraCode} — ${c.zone ?? ""} ${c.station ?? ""} (${c.status})`}>
            <span className="cam-dot">📷</span>
            <span className="cam-label">{c.cameraCode.replace(/^CAM-/, "")}</span>
          </button>
        ))}
        {placed.length === 0 && <div className="floorplan-hint">No cameras placed yet — pick one above and click the plan.</div>}
      </div>
      <p className="muted" style={{ fontSize: 12, marginTop: 8 }}>
        Positions are stored as fractions of the plan, so they stay correct at any screen size.
        Handheld cameras aren’t pinned — they move with the operator.
      </p>
    </div>
  );
}

function SiteMapPanel({ user, maps, mapId, setMapId, onDone, setMsg }: {
  user: CurrentUser; maps: SiteMapRow[]; mapId: number | "";
  setMapId: (v: number | "") => void; onDone: () => void; setMsg: (s: string) => void;
}) {
  const [site, setSite] = useState("LDN1");
  const [name, setName] = useState("London 1 — Dispatch Floor");
  const [imageUri, setImageUri] = useState("");

  async function create() {
    try {
      const r = await api.upsertSiteMap({ siteCode: site.trim(), name, imageUri: imageUri || undefined,
        width: 1000, height: 600 }, user.getToken());
      setMsg(`✅ Site map saved (id ${r.siteMapId})`);
      setMapId(r.siteMapId);
      onDone();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="card">
      <h3>Site map</h3>
      <div className="btn-row" style={{ marginBottom: 10 }}>
        <label className="inline-label">
          Showing
          <select value={mapId} onChange={(e) => setMapId(e.target.value === "" ? "" : +e.target.value)}>
            <option value="">None</option>
            {maps.map((m) => <option key={m.siteMapId} value={m.siteMapId}>{m.name}</option>)}
          </select>
        </label>
      </div>
      <div className="form-grid">
        <label>Site code<input value={site} onChange={(e) => setSite(e.target.value)} /></label>
        <label>Map name<input value={name} onChange={(e) => setName(e.target.value)} /></label>
        <label className="wide">Floor-plan image URL (optional — blank uses a grid)
          <input value={imageUri} onChange={(e) => setImageUri(e.target.value)} placeholder="https://…/floorplan.png" />
        </label>
      </div>
      <button className="primary" onClick={create}>Save site map</button>
    </div>
  );
}

function RegisterPanel({ user, onDone }: { user: CurrentUser; onDone: () => void }) {
  const [code, setCode] = useState("CAM-LDN1-PACK-02");
  const [name, setName] = useState("Pack bench 2 inspection");
  const [kind, setKind] = useState("Fixed");
  const [site, setSite] = useState("LDN1");
  const [zone, setZone] = useState("Pick Face");
  const [station, setStation] = useState("Pack Bench 2");
  const [checkpoint, setCheckpoint] = useState("PickTrayBuild");
  const [rtsp, setRtsp] = useState("rtsp://pack2.local/stream1");
  const [purpose, setPurpose] = useState("ItemCount");
  const [msg, setMsg] = useState("");

  async function save() {
    try {
      const r = await api.upsertCamera({ cameraCode: code.trim(), name, cameraKind: kind, siteCode: site.trim(),
        zone, station, checkpoint, rtspUrl: kind === "Handheld" ? undefined : rtsp, purpose, status: "Active" }, user.getToken());
      setMsg(`✅ ${code} registered (id ${r.cameraId})`);
      onDone();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="card">
      <h3>Register / update a camera</h3>
      <div className="form-grid">
        <label>Camera code<input value={code} onChange={(e) => setCode(e.target.value)} /></label>
        <label>Name<input value={name} onChange={(e) => setName(e.target.value)} /></label>
        <label>Kind
          <select value={kind} onChange={(e) => setKind(e.target.value)}>
            <option value="Fixed">Fixed — mounted at a station</option>
            <option value="Handheld">Handheld — phone / scanner camera</option>
          </select>
        </label>
        <label>Site<input value={site} onChange={(e) => setSite(e.target.value)} /></label>
        <label>Zone<input value={zone} onChange={(e) => setZone(e.target.value)} /></label>
        <label>Station<input value={station} onChange={(e) => setStation(e.target.value)} /></label>
        <label>Checkpoint
          <select value={checkpoint} onChange={(e) => setCheckpoint(e.target.value)}>
            <option value="PickTrayBuild">PickTrayBuild</option>
            <option value="DispatchDock">DispatchDock</option>
            <option value="VehicleLoad">VehicleLoad</option>
            <option value="StoreReceive">StoreReceive</option>
          </select>
        </label>
        <label>Purpose
          <select value={purpose} onChange={(e) => setPurpose(e.target.value)}>
            <option value="ItemCount">Item counting</option>
            <option value="CartonVerify">Carton verification</option>
            <option value="Both">Both</option>
          </select>
        </label>
        {kind === "Fixed" && (
          <label className="wide">RTSP stream URL<input value={rtsp} onChange={(e) => setRtsp(e.target.value)} /></label>
        )}
      </div>
      <div className="btn-row">
        <button className="primary" onClick={save}>Save camera</button>
        {msg && <span className="pill">{msg}</span>}
      </div>
    </div>
  );
}
