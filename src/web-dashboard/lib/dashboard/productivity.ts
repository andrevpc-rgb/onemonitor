import type { DeviceProductivity } from "@/lib/dashboard/aggregate";

export type ProductivityTier = "good" | "warning" | "critical";

/** % de tempo ativo sobre (ativo + inativo) — 0 quando não há nenhum dos dois ainda. */
export function productivityPercent(device: Pick<DeviceProductivity, "activeSeconds" | "idleSeconds">): number {
  const total = device.activeSeconds + device.idleSeconds;
  return total > 0 ? (device.activeSeconds / total) * 100 : 0;
}

/** Semáforo: >=90 bom, 75-89 atenção, <75 crítico — limiares pedidos para o grid de equipe. */
export function productivityTier(percent: number): ProductivityTier {
  if (percent >= 90) {
    return "good";
  }
  if (percent >= 75) {
    return "warning";
  }
  return "critical";
}

export const PRODUCTIVITY_TIER_COLOR: Record<ProductivityTier, string> = {
  good: "var(--viz-status-good)",
  warning: "var(--viz-status-warning)",
  critical: "var(--viz-status-critical)",
};
