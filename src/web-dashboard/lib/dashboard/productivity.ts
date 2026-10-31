import { NEUTRAL_TIME_WEIGHT } from "./productivityClassification";

export type ProductivityTier = "good" | "warning" | "critical";

/**
 * % de produtividade de um dispositivo sobre a jornada líquida alvo (mesma fórmula do Gauge
 * da equipe, ver TeamProductivityOverview) — null quando o
 * dispositivo não tem nenhum evento classificável no período, para distinguir "0%" (jornada
 * inteira improdutiva) de "sem dados" nos cards e no cálculo da média da equipe.
 */
export function deviceProductivityPercent(
  totals: { productiveSeconds: number; neutralSeconds: number; unproductiveSeconds: number },
  targetSeconds: number,
): number | null {
  const hasData = totals.productiveSeconds + totals.neutralSeconds + totals.unproductiveSeconds > 0;
  if (!hasData || targetSeconds <= 0) {
    return null;
  }
  return Math.min(100, ((totals.productiveSeconds + totals.neutralSeconds * NEUTRAL_TIME_WEIGHT) / targetSeconds) * 100);
}

/** Limiar "na meta" — também onde o tracinho de meta é desenhado no gauge (ver gauge.ts). */
export const GOOD_TIER_THRESHOLD = 90;
export const WARNING_TIER_THRESHOLD = 75;

/** Semáforo: >=90 bom, 75-89 atenção, <75 crítico — limiares pedidos para o grid de equipe. */
export function productivityTier(percent: number): ProductivityTier {
  if (percent >= GOOD_TIER_THRESHOLD) {
    return "good";
  }
  if (percent >= WARNING_TIER_THRESHOLD) {
    return "warning";
  }
  return "critical";
}

export const PRODUCTIVITY_TIER_COLOR: Record<ProductivityTier, string> = {
  good: "var(--viz-status-good)",
  warning: "var(--viz-status-warning)",
  critical: "var(--viz-status-critical)",
};

export const PRODUCTIVITY_TIER_LABELS: Record<ProductivityTier, string> = {
  good: "Na meta",
  warning: "Atenção",
  critical: "Crítico",
};
