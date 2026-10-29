import { PRODUCTIVITY_CLASS_COLOR, PRODUCTIVITY_CLASS_LABELS, NEUTRAL_TIME_WEIGHT } from "@/lib/dashboard/productivityClassification";
import { productivityTier, PRODUCTIVITY_TIER_COLOR } from "@/lib/dashboard/productivity";
import { formatDuration } from "@/lib/dashboard/format";

const RADIUS = 54;
const STROKE = 10;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * Gauge (donut) de produtividade — SVG puro, sem lib de gráficos (consistente com o resto
 * do dashboard desde a Fase 4).
 *
 * Fórmula: (tempo produtivo + tempo neutro * NEUTRAL_TIME_WEIGHT) / jornada líquida alvo.
 * Deliberadamente não é mais "ativo / (ativo + inativo)" — isso inflava a métrica com
 * qualquer janela em primeiro plano, incluindo navegação em pastas ou redes sociais. Tempo
 * neutro conta só pela metade (ver NEUTRAL_TIME_WEIGHT em productivityClassification.ts).
 * O denominador é a Jornada Líquida (ver lib/dashboard/workPolicy.ts) multiplicada pelo
 * número de dispositivos no escopo filtrado, não o tempo total do período — comparar contra
 * a meta de trabalho, não contra "o quanto a tela ficou ligada".
 */
export function ProductivityGauge({
  productiveSeconds,
  neutralSeconds,
  unproductiveSeconds,
  targetSeconds,
  periodLabel,
}: {
  productiveSeconds: number;
  neutralSeconds: number;
  unproductiveSeconds: number;
  targetSeconds: number;
  periodLabel: string;
}) {
  const hasData = productiveSeconds + neutralSeconds + unproductiveSeconds > 0;
  const percent =
    targetSeconds > 0
      ? Math.min(100, ((productiveSeconds + neutralSeconds * NEUTRAL_TIME_WEIGHT) / targetSeconds) * 100)
      : 0;
  const color = PRODUCTIVITY_TIER_COLOR[productivityTier(percent)];
  const dash = (percent / 100) * CIRCUMFERENCE;

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Produtividade da organização
      </h2>
      <p className="mb-4 text-xs" style={{ color: "var(--viz-text-muted)" }}>
        {periodLabel} · (produtivo + {NEUTRAL_TIME_WEIGHT * 100}% do neutro) sobre a jornada líquida alvo, todos os dispositivos
      </p>

      <div className="flex items-center gap-6">
        <svg viewBox="0 0 120 120" width={120} height={120} role="img" aria-label={`Produtividade: ${percent.toFixed(0)}%`}>
          <circle cx={60} cy={60} r={RADIUS} fill="none" stroke="var(--viz-gridline)" strokeWidth={STROKE} />
          {hasData && (
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
          <text x={60} y={56} textAnchor="middle" fontSize={24} fontWeight={700} fill="var(--viz-text-primary)">
            {hasData ? `${percent.toFixed(0)}%` : "—"}
          </text>
          <text x={60} y={74} textAnchor="middle" fontSize={10} fill="var(--viz-text-muted)">
            {hasData ? "da jornada alvo" : "sem dados"}
          </text>
        </svg>

        <dl className="space-y-1 text-xs" style={{ color: "var(--viz-text-secondary)" }}>
          <div className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: PRODUCTIVITY_CLASS_COLOR.productive }} aria-hidden />
            <dt className="inline">{PRODUCTIVITY_CLASS_LABELS.productive}</dt>
            <dd className="inline font-medium tabular-nums">{formatDuration(productiveSeconds)}</dd>
          </div>
          <div className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: PRODUCTIVITY_CLASS_COLOR.neutral }} aria-hidden />
            <dt className="inline">{PRODUCTIVITY_CLASS_LABELS.neutral}</dt>
            <dd className="inline font-medium tabular-nums">{formatDuration(neutralSeconds)}</dd>
          </div>
          <div className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: PRODUCTIVITY_CLASS_COLOR.unproductive }} aria-hidden />
            <dt className="inline">{PRODUCTIVITY_CLASS_LABELS.unproductive}</dt>
            <dd className="inline font-medium tabular-nums">{formatDuration(unproductiveSeconds)}</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}
