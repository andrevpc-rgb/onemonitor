import { formatDuration } from "@/lib/dashboard/format";
import { EmptyState } from "./EmptyState";

interface CategorySlice {
  category: string;
  seconds: number;
  color: string;
}

/**
 * Parte-todo com poucas categorias -> barra única empilhada horizontal, cor categórica
 * em ordem fixa (ver references/choosing-a-form.md do skill de dataviz). Gap de 2px na
 * cor de superfície entre segmentos; legenda sempre presente (>= 2 séries).
 */
export function CategoryBreakdownChart({ data }: { data: CategorySlice[] }) {
  const total = data.reduce((sum, d) => sum + d.seconds, 0);

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Distribuição de tempo por categoria
      </h2>

      {total === 0 ? (
        <EmptyState message="Nenhuma atividade registrada nas últimas 24 horas." />
      ) : (
        <>
          <div
            className="flex h-6 w-full overflow-hidden rounded-md"
            style={{ background: "var(--viz-surface)", border: "1px solid var(--viz-gridline)" }}
          >
            {data.map((item, index) => {
              const widthPct = (item.seconds / total) * 100;
              if (widthPct <= 0) {
                return null;
              }
              return (
                <div
                  key={item.category}
                  title={`${item.category}: ${formatDuration(item.seconds)} (${Math.round(widthPct)}%)`}
                  style={{
                    width: `${widthPct}%`,
                    background: item.color,
                    marginLeft: index === 0 ? 0 : "2px",
                  }}
                />
              );
            })}
          </div>

          <ul className="mt-4 flex flex-wrap gap-x-4 gap-y-2">
            {data.map((item) => (
              <li
                key={item.category}
                className="flex items-center gap-2 text-xs"
                style={{ color: "var(--viz-text-secondary)" }}
              >
                <span
                  className="inline-block h-2.5 w-2.5 rounded-full"
                  style={{ background: item.color }}
                  aria-hidden
                />
                <span>{item.category}</span>
                <span className="tabular-nums" style={{ color: "var(--viz-text-muted)" }}>
                  {formatDuration(item.seconds)} · {Math.round((item.seconds / total) * 100)}%
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
