import { useEffect, useState } from "react";
import { api, type ManifestRow } from "../api";
import type { CurrentUser } from "../auth";

export function Manifests({ user }: { user: CurrentUser }) {
  const [trayQr, setTrayQr] = useState("TRAY-LDN1-000001");
  const [count, setCount] = useState(10);
  const [payloads, setPayloads] = useState("0109501234567891 21A, 0109501234567891 21B");
  const [rows, setRows] = useState<ManifestRow[]>([]);
  const [msg, setMsg] = useState("");

  const load = () => api.manifests("2000-01-01T00:00:00Z", user.getToken()).then((r) => setRows(r.manifests)).catch(() => setRows([]));
  useEffect(() => { load(); }, []);

  async function upsert() {
    try {
      await api.upsertManifest({ trayQr, expectedCartonCount: count,
        expectedCartonPayloads: payloads.split(",").map((s) => s.trim()).filter(Boolean) }, user.getToken());
      setMsg(`✅ Manifest for ${trayQr} saved`); load();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="view">
      <h2>Tray Manifests (ASN)</h2>
      <p className="muted">Expected tray contents synced to the dock camera + used for receiving reconciliation. Persists to Azure SQL.</p>

      <div className="card">
        <h3>Upsert manifest</h3>
        <div className="form-grid">
          <label>Tray QR<input value={trayQr} onChange={(e) => setTrayQr(e.target.value)} /></label>
          <label>Expected cartons<input type="number" value={count} onChange={(e) => setCount(+e.target.value)} /></label>
          <label className="wide">Carton payloads (comma-separated)<input value={payloads} onChange={(e) => setPayloads(e.target.value)} /></label>
        </div>
        <button className="primary" onClick={upsert}>Save manifest</button>
        {msg && <span className="pill">{msg}</span>}
      </div>

      <div className="card">
        <h3>All manifests</h3>
        <table className="mini-grid">
          <thead><tr><th>Tray</th><th>Expected</th><th>Payloads</th><th>Updated</th></tr></thead>
          <tbody>
            {rows.map((m) => (
              <tr key={m.trayQr}><td>{m.trayQr}</td><td>{m.expectedCartonCount}</td>
                <td className="detail">{m.expectedCartonPayloads?.join(", ")}</td>
                <td>{new Date(m.updatedUtc).toLocaleString()}</td></tr>
            ))}
            {rows.length === 0 && <tr><td colSpan={4} className="empty">No manifests yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
