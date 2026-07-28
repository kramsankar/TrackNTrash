import type { ConsoleException } from "../types";

const sevRank: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

export function ExceptionGrid({
  items,
  selectedId,
  onSelect,
}: {
  items: ConsoleException[];
  selectedId?: number;
  onSelect: (id: number) => void;
}) {
  const sorted = [...items].sort(
    (a, b) => (sevRank[a.severity] ?? 9) - (sevRank[b.severity] ?? 9) || b.ageMinutes - a.ageMinutes
  );

  return (
    <table className="grid">
      <thead>
        <tr>
          <th>Sev</th><th>Type</th><th>Checkpoint</th><th>Status</th><th>Age</th><th>Detail</th>
        </tr>
      </thead>
      <tbody>
        {sorted.map((e) => (
          <tr
            key={e.id}
            className={`${e.id === selectedId ? "sel" : ""} sev-${e.severity.toLowerCase()}`}
            onClick={() => onSelect(e.id)}
          >
            <td><span className={`badge sev-${e.severity.toLowerCase()}`}>{e.severity}</span></td>
            <td>{e.type}</td>
            <td>{e.checkpoint ?? "—"}</td>
            <td>{e.status}</td>
            <td>{formatAge(e.ageMinutes)}</td>
            <td className="detail">{e.detail}</td>
          </tr>
        ))}
        {sorted.length === 0 && (
          <tr><td colSpan={6} className="empty">No exceptions match the current filters 🎉</td></tr>
        )}
      </tbody>
    </table>
  );
}

function formatAge(min: number): string {
  if (min < 60) return `${min}m`;
  if (min < 1440) return `${Math.floor(min / 60)}h`;
  return `${Math.floor(min / 1440)}d`;
}
