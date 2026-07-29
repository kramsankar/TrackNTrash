import { useCallback, useEffect, useState } from "react";
import QRCode from "qrcode";
import { api, type AssetRow, type AssetSummary } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category, date } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";

const STATUS_COLORS: Record<string, string> = {
  Available: "green", InUse: "blue", InTransit: "violet", AtStore: "amber",
  Maintenance: "slate", Lost: "red", WrittenOff: "red",
};

const columns = [
  text<AssetRow>("trayQr", "Tray QR", { pinned: "left" }),
  category<AssetRow>("siteCode", "Site"),
  badge<AssetRow>("trayStatus", "Status", STATUS_COLORS),
  category<AssetRow>("currentCustodianType", "Custodian"),
  text<AssetRow>("currentCustodianRef", "Held by"),
  date<AssetRow>("createdUtc", "Registered"),
];

export function Assets({ user }: { user: CurrentUser }) {
  const [rows, setRows] = useState<AssetRow[]>([]);
  const [summary, setSummary] = useState<AssetSummary | null>(null);
  const [selected, setSelected] = useState<AssetRow[]>([]);
  const [site, setSite] = useState("LDN1");
  const [count, setCount] = useState(6);
  const [msg, setMsg] = useState("");
  const [qr, setQr] = useState<{ tray: string; png: string } | null>(null);

  const refresh = useCallback(() => {
    api.listAssets(user.getToken()).then(setRows).catch(() => setRows([]));
    api.assetSummary(user.getToken()).then(setSummary).catch(() => setSummary(null));
  }, [user]);
  useEffect(() => { refresh(); }, [refresh]);

  async function register() {
    try {
      const r = await api.registerAssets(site.trim(), count, user.getToken());
      setMsg(`✅ Registered ${r.registered} trays: ${r.trayQrs.slice(0, 3).join(", ")}${r.trayQrs.length > 3 ? "…" : ""}`);
      refresh();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  async function showQr(tray: string) {
    const png = await QRCode.toDataURL(tray, { errorCorrectionLevel: "H", margin: 1, width: 240 });
    setQr({ tray, png });
  }
  async function downloadSvg(tray: string) {
    const svg = await QRCode.toString(tray, { type: "svg", errorCorrectionLevel: "H", margin: 1 });
    dl(`${tray}.svg`, "data:image/svg+xml;utf8," + encodeURIComponent(svg));
  }
  function dl(name: string, href: string) {
    const a = document.createElement("a"); a.href = href; a.download = name; a.click();
  }

  async function printSheet() {
    const list = selected.length ? selected : rows;
    if (!list.length) return;
    const cells = await Promise.all(list.map(async (a) => {
      const png = await QRCode.toDataURL(a.trayQr, { errorCorrectionLevel: "H", margin: 1, width: 220 });
      return `<div class="c"><img src="${png}"/><div class="l">${a.trayQr}</div></div>`;
    }));
    const w = window.open("", "_blank");
    if (!w) return;
    w.document.write(`<html><head><title>Tray QR sheet</title><style>
      body{font-family:system-ui;margin:16px} .g{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}
      .c{border:1px solid #ccc;border-radius:8px;padding:10px;text-align:center;break-inside:avoid}
      .c img{width:100%;max-width:200px} .l{font:600 13px monospace;margin-top:6px}
      @media print{.noprint{display:none}}</style></head><body>
      <button class="noprint" onclick="print()">Print</button>
      <h3>TrackNTrash — Tray QR labels (${list.length})</h3><div class="g">${cells.join("")}</div></body></html>`);
    w.document.close();
  }

  const kpis = summary ? [
    { k: "Total", v: summary.total, t: "" }, { k: "Available", v: summary.available, t: "good" },
    { k: "In use", v: summary.inUse, t: "" }, { k: "In transit", v: summary.inTransit, t: "" },
    { k: "At store", v: summary.atStore, t: "warn" }, { k: "Lost", v: summary.lost, t: summary.lost ? "bad" : "good" },
  ] : [];

  return (
    <div className="view">
      <div className="view-head">
        <div><h2>Asset Master — Trays</h2><p className="muted">Reusable trays and their QR labels.</p></div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      {summary && <div className="kpi-row">{kpis.map((k) => (
        <div key={k.k} className={`kpi ${k.t}`}><div className="kpi-value">{k.v}</div><div className="kpi-label">{k.k}</div></div>
      ))}</div>}

      <div className="card">
        <h3>Register trays &amp; generate QR</h3>
        <div className="form-grid">
          <label>Site code<input value={site} onChange={(e) => setSite(e.target.value)} /></label>
          <label>How many<input type="number" value={count} onChange={(e) => setCount(+e.target.value)} /></label>
        </div>
        <div className="btn-row">
          <button className="primary" onClick={register}>Register trays</button>
          <button onClick={printSheet}>🖨 Print QR sheet {selected.length ? `(${selected.length} selected)` : "(all)"}</button>
          {msg && <span className="pill">{msg}</span>}
        </div>
      </div>

      <div className="grid-wrap">
        {rows.length === 0
          ? <div className="empty">No trays yet — register some above.</div>
          : <EnterpriseGrid<AssetRow> columns={columns} rows={rows} getRowId="trayId" height={340}
              selection="multiple" stateKey="tnt-assets" exportFileName="trays"
              onSelectionChanged={setSelected} onRowClick={(r: AssetRow) => showQr(r.trayQr)} />}
      </div>

      {qr && (
        <div className="card qr-card">
          <h3>QR · {qr.tray}</h3>
          <img src={qr.png} alt={qr.tray} width={200} height={200} style={{ background: "#fff", borderRadius: 8, padding: 8 }} />
          <div className="btn-row" style={{ marginTop: 10 }}>
            <button onClick={() => dl(`${qr.tray}.png`, qr.png)}>Download PNG</button>
            <button onClick={() => downloadSvg(qr.tray)}>Download SVG (laser-etch)</button>
            <button onClick={() => setQr(null)}>Close</button>
          </div>
          <p className="muted" style={{ marginTop: 6, fontSize: 12 }}>Click any row to preview its QR. Select rows and “Print QR sheet” for a printable label page.</p>
        </div>
      )}
    </div>
  );
}
