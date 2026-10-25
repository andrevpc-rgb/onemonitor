import { formatDuration } from "@/lib/dashboard/format";
import type { AggregatedUsage } from "@/lib/dashboard/aggregate";
import { EmptyState } from "./EmptyState";

/**
 * Comparação de magnitude (tempo por app) -> bar chart, hue sequencial único (ver
 * references/choosing-a-form.md do skill de dataviz: "Compare magnitude -> bar, cor
 * sequencial"). Barra cresce da baseline (esquerda), 4px arredondado só na ponta.
 */
export function AppUsageChart({ data }: { data: AggregatedUsage[] }) {
  if (data.length === 0) {
    return (
      <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
        <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
          Tempo ativo por aplicativo
        </h2>
        <EmptyState message="Nenhuma atividade registrada nas últimas 24 horas." />
      </section>
    );
  }

  const max = Math.max(...data.map((d) => d.seconds));

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Tempo ativo por aplicativo
      </h2>
      <div className="space-y-3">
        {data.map((item) => (
          <div key={item.label} className="flex items-center gap-3">
            <span
              className="w-28 shrink-0 truncate text-xs"
              style={{ color: "var(--viz-text-secondary)" }}
              title={item.label}
            >
              {item.label}
            </span>
            <div className="h-2 flex-1" style={{ background: "var(--viz-gridline)" }}>
              <div
                className="h-2"
                title={formatDuration(item.seconds)}
                style={{
                  width: `${Math.max(1.5, (item.seconds / max) * 100)}%`,
                  background: "var(--viz-sequential)",
                  borderRadius: "0 4px 4px 0",
                }}
              />
            </div>
            <span
              className="w-16 shrink-0 text-right text-xs tabular-nums"
              style={{ color: "var(--viz-text-secondary)" }}
            >
              {formatDuration(item.seconds)}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}
