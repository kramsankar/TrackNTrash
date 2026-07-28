import { useEffect, useState } from "react";
import { api } from "../api";
import type { ConsoleException } from "../types";
import type { CurrentUser } from "../auth";

export function Dashboard({ user, onNavigate }: { user: CurrentUser; onNavigate: (v: string) => void }) {
  const [exceptions, setExceptions] = useState<ConsoleException[]>([]);
  const [health, setHealth] = useState<string>("…");

  useEffect(() => {
    api.openExceptions(user.getToken()).then(setExceptions).catch(() => setExceptions([]));
    api.health().then((h) => setHealth(h.status)).catch(() => setHealth("unreachable"));
  }, [user]);

  const open = exceptions.filter((e) => e.status === "Open").length;
  const critical = exceptions.filter((e) => e.severity === "Critical").length;
  const high = exceptions.filter((e) => e.severity === "High").length;

  const bySev = ["Critical", "High", "Medium", "Low"].map((s) => ({ s, n: exceptions.filter((e) => e.severity === s).length }));

  return (
    <div className="view">
      <h2>Operations Dashboard</h2>
      <div className="kpi-row">
        <Kpi label="API health" value={health} tone={health === "ok" ? "good" : "bad"} />
        <Kpi label="Open exceptions" value={String(open)} tone={open ? "warn" : "good"} onClick={() => onNavigate("exceptions")} />
        <Kpi label="Critical" value={String(critical)} tone={critical ? "bad" : "good"} />
        <Kpi label="High" value={String(high)} tone={high ? "warn" : "good"} />
      </div>

      <h3>Exceptions by severity</h3>
      <div className="bars">
        {bySev.map(({ s, n }) => (
          <div key={s} className="bar-row">
            <span className={`bar-label sev-${s.toLowerCase()}`}>{s}</span>
            <div className="bar-track"><div className={`bar-fill sev-${s.toLowerCase()}`} style={{ width: `${Math.min(100, n * 12 + (n ? 8 : 0))}%` }} /></div>
            <span className="bar-num">{n}</span>
          </div>
        ))}
      </div>

      <h3>Recent exceptions</h3>
      <table className="mini-grid">
        <thead><tr><th>Sev</th><th>Type</th><th>Checkpoint</th><th>Detail</th></tr></thead>
        <tbody>
          {exceptions.slice(0, 6).map((e) => (
            <tr key={e.id} onClick={() => onNavigate("exceptions")}>
              <td><span className={`badge sev-${e.severity.toLowerCase()}`}>{e.severity}</span></td>
              <td>{e.type}</td><td>{e.checkpoint ?? "—"}</td><td className="detail">{e.detail}</td>
            </tr>
          ))}
          {exceptions.length === 0 && <tr><td colSpan={4} className="empty">No open exceptions 🎉</td></tr>}
        </tbody>
      </table>
    </div>
  );
}

function Kpi({ label, value, tone, onClick }: { label: string; value: string; tone: "good" | "warn" | "bad"; onClick?: () => void }) {
  return (
    <div className={`kpi ${tone} ${onClick ? "click" : ""}`} onClick={onClick}>
      <div className="kpi-value">{value}</div>
      <div className="kpi-label">{label}</div>
    </div>
  );
}
