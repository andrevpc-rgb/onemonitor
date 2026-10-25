import { formatDuration, formatDateTime } from "@/lib/dashboard/format";
import type { JustifiedAbsence } from "@/lib/dashboard/aggregate";
import { EmptyState } from "./EmptyState";

export function JustifiedAbsencesTable({ data }: { data: JustifiedAbsence[] }) {
  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Ausências justificadas
      </h2>
      {data.length === 0 ? (
        <EmptyState message="Nenhuma ausência justificada nas últimas 24 horas." />
      ) : (
        <table className="w-full text-left text-xs">
          <thead>
            <tr style={{ color: "var(--viz-text-muted)", borderBottom: "1px solid var(--viz-gridline)" }}>
              <th className="py-2 pr-3 font-normal">Horário</th>
              <th className="py-2 pr-3 font-normal">Motivo</th>
              <th className="py-2 pr-3 font-normal">Janela</th>
              <th className="py-2 text-right font-normal tabular-nums">Duração</th>
            </tr>
          </thead>
          <tbody>
            {data.map((item, index) => (
              <tr key={`${item.startedAt}-${index}`} style={{ borderBottom: "1px solid var(--viz-gridline)" }}>
                <td className="py-2 pr-3 tabular-nums" style={{ color: "var(--viz-text-secondary)" }}>
                  {formatDateTime(item.startedAt)}
                </td>
                <td className="py-2 pr-3" style={{ color: "var(--viz-text-primary)" }}>
                  {item.justification}
                </td>
                <td
                  className="max-w-[220px] truncate py-2 pr-3"
                  style={{ color: "var(--viz-text-secondary)" }}
                  title={item.windowTitle}
                >
                  {item.windowTitle || "—"}
                </td>
                <td className="py-2 text-right tabular-nums" style={{ color: "var(--viz-text-secondary)" }}>
                  {formatDuration(item.seconds)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
