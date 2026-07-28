import { useState } from "react";
import { api, type LineState } from "../api";
import type { CurrentUser } from "../auth";

const STATES = ["Ordered", "Picked", "Staged", "Loaded", "InTransit", "Received"];

export function LineLookup({ user }: { user: CurrentUser }) {
  const [id, setId] = useState(1);
  const [state, setState] = useState<LineState | null>(null);
  const [err, setErr] = useState("");

  async function lookup() {
    setErr(""); setState(null);
    try { setState(await api.lineState(id, user.getToken())); }
    catch (e) { setErr((e as Error).message); }
  }

  const idx = state ? STATES.indexOf(state.currentState) : -1;

  return (
    <div className="view">
      <h2>Shipment Line Lookup</h2>
      <div className="card">
        <div className="form-grid">
          <label>Order line id<input type="number" value={id} onChange={(e) => setId(+e.target.value)} /></label>
        </div>
        <button className="primary" onClick={lookup}>Look up state</button>
        {err && <span className="pill bad">{err}</span>}
      </div>

      {state && (
        <div className="card">
          <h3>Line {state.orderLineId} · <span className="pill">{state.currentState}</span></h3>
          <div className="stepper">
            {STATES.map((s, i) => (
              <div key={s} className={`step ${i <= idx ? "done" : ""} ${i === idx ? "current" : ""}`}>
                <div className="dot" /><span>{s}</span>
              </div>
            ))}
          </div>
          <table className="mini-grid">
            <tbody>
              <tr><td>Current state</td><td>{state.currentState}</td></tr>
              <tr><td>Previous</td><td>{state.previousState ?? "—"}</td></tr>
              <tr><td>Picked cartons</td><td>{state.pickedCartons}</td></tr>
              <tr><td>Received cartons</td><td>{state.receivedCartons}</td></tr>
              <tr><td>Last event id</td><td>{state.lastEventId ?? "—"}</td></tr>
              <tr><td>State entered</td><td>{new Date(state.stateEnteredUtc).toLocaleString()}</td></tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
