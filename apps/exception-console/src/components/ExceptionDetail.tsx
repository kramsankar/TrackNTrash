import { useEffect, useState } from "react";
import { api, API_BASE } from "../api";
import type { ExceptionDetail } from "../types";
import { canEscalate, canResolve, type CurrentUser } from "../auth";

export function ExceptionDetailPane({
  id,
  user,
  onChanged,
}: {
  id: number;
  user: CurrentUser;
  onChanged: () => void;
}) {
  const [detail, setDetail] = useState<ExceptionDetail | null>(null);
  const [reason, setReason] = useState("Resolved-verified");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setDetail(null);
    api.get(id, user.getToken()).then(setDetail).catch(console.error);
  }, [id]);

  if (!detail) return <div className="detail-pane">Loading…</div>;
  const e = detail.exception;
  const media = e.frameBlobUri ?? e.photoBlobUri;

  async function act(fn: () => Promise<unknown>) {
    setBusy(true);
    try { await fn(); await api.get(id, user.getToken()).then(setDetail); onChanged(); }
    finally { setBusy(false); }
  }

  return (
    <div className="detail-pane">
      <h2>#{e.id} · {e.type} <span className={`badge sev-${e.severity.toLowerCase()}`}>{e.severity}</span></h2>
      <p className="muted">{e.checkpoint} · {e.status} · {e.detail}</p>

      {media && (
        <figure>
          <img src={`${API_BASE}/blob?uri=${encodeURIComponent(media)}`} alt="exception evidence" />
          <figcaption>{e.frameBlobUri ? "Dock camera frame" : "Receiving photo"}</figcaption>
        </figure>
      )}

      <h3>Actions</h3>
      <div className="actions">
        <button disabled={busy || e.status !== "Open"} onClick={() => act(() => api.acknowledge(e.id, user.upn, user.getToken()))}>
          Acknowledge
        </button>
        <button disabled={busy || !canEscalate(user)} onClick={() => act(() => api.escalate(e.id, user.upn, user.getToken()))}>
          Escalate → Teams
        </button>
        <div className="resolve">
          <input value={reason} onChange={(ev) => setReason(ev.target.value)} placeholder="Reason code" />
          <input value={note} onChange={(ev) => setNote(ev.target.value)} placeholder="Note" />
          <button disabled={busy || !canResolve(user)} onClick={() => act(() => api.resolve(e.id, user.upn, reason, note, user.getToken()))}>
            Resolve
          </button>
        </div>
      </div>

      <h3>Event timeline</h3>
      <ol className="timeline">
        {detail.timeline.map((t) => (
          <li key={t.scanEventId}>
            <b>{t.eventType}</b> {t.verdict ? `(${t.verdict})` : ""} · <span className="muted">{new Date(t.eventUtc).toLocaleString()}</span>
          </li>
        ))}
        {detail.timeline.length === 0 && <li className="muted">No linked order-line events.</li>}
      </ol>

      <h3>Audit</h3>
      <ul className="audit">
        {e.audit.map((a, i) => (
          <li key={i}>{a.action} · {a.user} · {new Date(a.utc).toLocaleString()}{a.note ? ` · ${a.note}` : ""}</li>
        ))}
        {e.audit.length === 0 && <li className="muted">No actions yet.</li>}
      </ul>
    </div>
  );
}
