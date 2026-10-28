import { productivityTier, PRODUCTIVITY_TIER_COLOR } from "@/lib/dashboard/productivity";
import { formatDuration } from "@/lib/dashboard/format";

const RADIUS = 54;
const STROKE = 10;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * Gauge (donut) de produtividade — SVG puro, sem lib de gráficos (consistente com o resto
 * do dashboard desde a Fase 4). Um único valor real (ativo / (ativo + inativo) no período
 * filtrado); sem "meta" comparativa porque a organização não tem essa configuração ainda —
 * ver conversa da Fase 8.x antes de inventar um alvo que não existe no banco.
 */
export function ProductivityGauge({
  activeSeconds,
  idleSeconds,
  periodLabel,
}: {
  activeSeconds: number;
  idleSeconds: number;
  periodLabel: string;
}) {
  const total = activeSeconds + idleSeconds;
  const percent = total > 0 ? (activeSeconds / total) * 100 : 0;
  const tier = productivityTier(percent);
  const color = PRODUCTIVITY_TIER_COLOR[tier];
  const dash = (percent / 100) * CIRCUMFERENCE;

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Produtividade da organização
      </h2>
      <p className="mb-4 text-xs" style={{ color: "var(--viz-text-muted)" }}>
        {periodLabel} · tempo ativo sobre ativo + inativo, todos os dispositivos
      </p>

      <div className="flex items-center gap-6">
        <svg viewBox="0 0 120 120" width={120} height={120} role="img" aria-label={`Produtividade: ${percent.toFixed(0)}%`}>
          <circle cx={60} cy={60} r={RADIUS} fill="none" stroke="var(--viz-gridline)" strokeWidth={STROKE} />
          {total > 0 && (
            <circle
              cx={60}
              cy={60}
              r={RADIUS}
              fill="none"
              stroke={color}
              strokeWidth={STROKE}
              strokeLinecap="round"
              strokeDasharray={`${dash} ${CIRCUMFERENCE}`}
              transform="rotate(-90 60 60)"
            />
          )}
          <text
            x={60}
            y={56}
            textAnchor="middle"
            fontSize={24}
            fontWeight={700}
            fill="var(--viz-text-primary)"
          >
            {total > 0 ? `${percent.toFixed(0)}%` : "—"}
          </text>
          <text x={60} y={74} textAnchor="middle" fontSize={10} fill="var(--viz-text-muted)">
            {total > 0 ? "produtivo" : "sem dados"}
          </text>
        </svg>

        <dl className="space-y-1 text-xs" style={{ color: "var(--viz-text-secondary)" }}>
          <div className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: color }} aria-hidden />
            <dt className="inline">Ativo</dt>
            <dd className="inline font-medium tabular-nums">{formatDuration(activeSeconds)}</dd>
          </div>
          <div className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: "var(--viz-gridline)" }} aria-hidden />
            <dt className="inline">Inativo</dt>
            <dd className="inline font-medium tabular-nums">{formatDuration(idleSeconds)}</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}
