import { AlertTriangle } from "lucide-react";
import type { WorkloadRiskEntry } from "@/lib/dashboard/workloadRisk";

/**
 * Indicador de risco de sobrecarga (NR-1/GRO) — só renderiza quando há pelo menos um
 * dispositivo em sequência de dias acima do limiar configurado em /settings. Cor de aviso
 * (nunca crítica): é um sinal pra gestão olhar com atenção, não uma falha do sistema.
 */
export function WorkloadRiskAlert({ entries }: { entries: WorkloadRiskEntry[] }) {
  if (entries.length === 0) {
    return null;
  }

  return (
    <section
      className="viz-root rounded-lg p-4"
      style={{ background: "var(--viz-surface)", border: "1px solid var(--viz-status-warning)" }}
      aria-label="Alerta de risco de sobrecarga de trabalho"
    >
      <div className="flex items-start gap-3">
        <AlertTriangle size={18} style={{ color: "var(--viz-status-warning)" }} className="mt-0.5 shrink-0" aria-hidden />
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
            Risco de sobrecarga de trabalho (NR-1 / GRO)
          </h2>
          <p className="mt-0.5 text-xs" style={{ color: "var(--viz-text-muted)" }}>
            Carga horária acima do limiar configurado em todos os últimos dias avaliados — ajuste o critério em Configurações.
          </p>

          <ul className="mt-3 space-y-2">
            {entries.map((entry) => (
              <li key={entry.deviceId} className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
                <span className="font-medium" style={{ color: "var(--viz-text-primary)" }}>
                  {entry.label}
                </span>
                <span style={{ color: "var(--viz-text-secondary)" }}>
                  {entry.dayLabels.map((day, i) => `${day}: ${entry.dailyHours[i].toFixed(1)}h`).join(" · ")}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}
