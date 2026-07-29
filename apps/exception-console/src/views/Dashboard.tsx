import { useEffect, useMemo, useState } from "react";
import { api, type AssetSummary, type OrderRow } from "../api";
import type { ConsoleException } from "../types";
import type { CurrentUser } from "../auth";
import { EnterpriseChart } from "../vendor/enterprise-charts";

const PIPELINE = ["Ordered", "Picked", "Staged", "Loaded", "InTransit", "Received"];

export function Dashboard({ items, onNavigate, user }:
  { items: ConsoleException[]; onNavigate: (v: string) => void; user: CurrentUser }) {
  const [health, setHealth] = useState("…");
  const [orders, setOrders] = useState<OrderRow[]>([]);
  const [assets, setAssets] = useState<AssetSummary | null>(null);

  useEffect(() => {
    api.health().then((h) => setHealth(h.status)).catch(() => setHealth("unreachable"));
    api.listOrders(user.getToken()).then(setOrders).catch(() => setOrders([]));
    api.assetSummary(user.getToken()).then(setAssets).catch(() => setAssets(null));
  }, [user]);

  const open = items.filter((e) => e.status === "Open").length;
  const critical = items.filter((e) => e.severity === "Critical").length;
  const received = orders.filter((o) => o.currentState === "Received").length;
  const accuracy = orders.length ? Math.round((received / orders.length) * 100) : 100;

  // Order lines by pipeline stage — a funnel of where work sits right now.
  const pipeline = useMemo(
    () => PIPELINE.map((s) => ({ stage: s, lines: orders.filter((o) => o.currentState === s).length })),
    [orders]);

  // Exceptions by severity.
  const bySeverity = useMemo(() => ["Critical", "High", "Medium", "Low"]
    .map((s) => ({ severity: s, count: items.filter((e) => e.severity === s).length }))
    .filter((d) => d.count > 0), [items]);

  // Exceptions by checkpoint.
  const byCheckpoint = useMemo(() => {
    const map = new Map<string, number>();
    for (const e of items) map.set(e.checkpoint ?? "Unknown", (map.get(e.checkpoint ?? "Unknown") ?? 0) + 1);
    return [...map].map(([checkpoint, count]) => ({ checkpoint, count }));
  }, [items]);

  // Tray fleet status.
  const trayMix = useMemo(() => assets ? [
    { status: "Available", trays: assets.available }, { status: "In use", trays: assets.inUse },
    { status: "In transit", trays: assets.inTransit }, { status: "At store", trays: assets.atStore },
    { status: "Lost", trays: assets.lost },
  ].filter((d) => d.trays > 0) : [], [assets]);

  return (
    <div className="view">
      <div className="view-head">
        <div>
          <h2>Operations Dashboard</h2>
          <p className="muted">Live picture of dispatch accuracy, work in flight and the tray fleet.</p>
        </div>
      </div>

      <div className="kpi-row">
        <Kpi label="API health" value={health} tone={health === "ok" ? "good" : "bad"} />
        <Kpi label="Dispatch accuracy" value={`${accuracy}%`} tone={accuracy >= 95 ? "good" : accuracy >= 80 ? "warn" : "bad"} />
        <Kpi label="Open exceptions" value={String(open)} tone={open ? "warn" : "good"} onClick={() => onNavigate("exceptions")} />
        <Kpi label="Critical" value={String(critical)} tone={critical ? "bad" : "good"} onClick={() => onNavigate("exceptions")} />
        <Kpi label="Order lines" value={String(orders.length)} tone="" onClick={() => onNavigate("orders")} />
        <Kpi label="Trays" value={String(assets?.total ?? "—")} tone="" onClick={() => onNavigate("assets")} />
      </div>

      <div className="chart-grid">
        <div className="chart-card">
          <EnterpriseChart type="column" data={pipeline} xKey="stage" yKeys={["lines"]}
            height={260} title="Work in flight — order lines by stage" toolbar />
        </div>

        <div className="chart-card">
          {bySeverity.length
            ? <EnterpriseChart type="donut" data={bySeverity} xKey="severity" yKeys={["count"]}
                height={260} title="Exceptions by severity" toolbar />
            : <Empty title="Exceptions by severity" msg="No open exceptions 🎉" />}
        </div>

        <div className="chart-card">
          {byCheckpoint.length
            ? <EnterpriseChart type="bar" data={byCheckpoint} xKey="checkpoint" yKeys={["count"]}
                height={260} title="Exceptions by checkpoint" toolbar />
            : <Empty title="Exceptions by checkpoint" msg="Nothing flagged at any checkpoint." />}
        </div>

        <div className="chart-card">
          {trayMix.length
            ? <EnterpriseChart type="pie" data={trayMix} xKey="status" yKeys={["trays"]}
                height={260} title="Tray fleet status" toolbar />
            : <Empty title="Tray fleet status" msg="No trays registered yet." />}
        </div>
      </div>

      <h3>Recent exceptions</h3>
      <div className="tbl-scroll">
        <table className="mini-grid">
          <thead><tr><th>Sev</th><th>Type</th><th>Checkpoint</th><th>Detail</th></tr></thead>
          <tbody>
            {items.slice(0, 6).map((e) => (
              <tr key={e.id} onClick={() => onNavigate("exceptions")}>
                <td><span className={`badge sev-${e.severity.toLowerCase()}`}>{e.severity}</span></td>
                <td>{e.type}</td><td>{e.checkpoint ?? "—"}</td><td className="detail">{e.detail}</td>
              </tr>
            ))}
            {items.length === 0 && <tr><td colSpan={4} className="empty">No open exceptions 🎉</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Empty({ title, msg }: { title: string; msg: string }) {
  return (
    <div className="chart-empty">
      <div className="chart-empty-title">{title}</div>
      <div className="muted">{msg}</div>
    </div>
  );
}

function Kpi({ label, value, tone, onClick }:
  { label: string; value: string; tone: "good" | "warn" | "bad" | ""; onClick?: () => void }) {
  return (
    <div className={`kpi ${tone} ${onClick ? "click" : ""}`} onClick={onClick}>
      <div className="kpi-value">{value}</div>
      <div className="kpi-label">{label}</div>
    </div>
  );
}
