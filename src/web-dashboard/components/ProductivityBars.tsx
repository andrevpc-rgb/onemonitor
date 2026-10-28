import type { DeviceProductivity } from "@/lib/dashboard/aggregate";
import { formatDuration } from "@/lib/dashboard/format";
import { formatMinutesAsHours } from "@/lib/dashboard/workPolicy";
import { EmptyState } from "./EmptyState";

const ACTIVE_COLOR = "var(--viz-series-1)";
const IDLE_COLOR = "var(--viz-series-2)";

/**
 * Ativo vs. inativo por dispositivo — part-to-whole com 2 categorias -> barra empilhada
 * horizontal, uma por dispositivo (small multiples), cor categórica fixa (ver
 * references/choosing-a-form.md do skill de dataviz). Só 2 séries: direct-label é
 * suficiente, sem precisar de legenda por linha — uma legenda só, compartilhada.
 */
export function ProductivityBars({
  data,
  targetMinutes,
}: {
  data: DeviceProductivity[];
  /** Jornada líquida alvo (expediente menos pausas) — ver lib/dashboard/workPolicy.ts. */
  targetMinutes: number;
}) {
  const hasAnyData = data.some((d) => d.activeSeconds + d.idleSeconds > 0);

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Ativo vs. inativo por dispositivo
      </h2>
      <p className="mb-4 text-xs" style={{ color: "var(--viz-text-muted)" }}>
        Jornada líquida alvo da organização: {formatMinutesAsHours(targetMinutes)}
      </p>

      {!hasAnyData ? (
        <EmptyState message="Nenhuma atividade registrada no período." />
      ) : (
        <>
          <div className="space-y-3">
            {data.map((device) => {
              const total = device.activeSeconds + device.idleSeconds;
              const activePct = total > 0 ? (device.activeSeconds / total) * 100 : 0;
              const idlePct = total > 0 ? (device.idleSeconds / total) * 100 : 0;

              return (
                <div key={device.deviceId} className="flex items-center gap-3">
                  <span
                    className="w-28 shrink-0 truncate text-xs"
                    style={{ color: "var(--viz-text-secondary)" }}
                    title={device.label}
                  >
                    {device.label}
                  </span>
                  <div
                    className="flex h-5 flex-1 overflow-hidden rounded-sm"
                    style={{ background: "var(--viz-gridline)" }}
                  >
                    {activePct > 0 && (
                      <div
                        title={`Ativo: ${formatDuration(device.activeSeconds)}`}
                        style={{ width: `${activePct}%`, background: ACTIVE_COLOR }}
                      />
                    )}
                    {idlePct > 0 && (
                      <div
                        title={`Inativo: ${formatDuration(device.idleSeconds)}`}
                        style={{
                          width: `${idlePct}%`,
                          background: IDLE_COLOR,
                          marginLeft: activePct > 0 ? "2px" : 0,
                        }}
                      />
                    )}
                  </div>
                  <span
                    className="w-16 shrink-0 text-right text-xs tabular-nums"
                    style={{ color: "var(--viz-text-secondary)" }}
                  >
                    {formatDuration(total)}
                  </span>
                </div>
              );
            })}
          </div>

          <ul className="mt-4 flex flex-wrap gap-x-4 gap-y-2">
            <li className="flex items-center gap-2 text-xs" style={{ color: "var(--viz-text-secondary)" }}>
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: ACTIVE_COLOR }} aria-hidden />
              <span>Ativo</span>
            </li>
            <li className="flex items-center gap-2 text-xs" style={{ color: "var(--viz-text-secondary)" }}>
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: IDLE_COLOR }} aria-hidden />
              <span>Inativo</span>
            </li>
          </ul>
        </>
      )}
    </section>
  );
}
