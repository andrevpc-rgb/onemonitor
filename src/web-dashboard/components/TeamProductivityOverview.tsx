import type { DeviceRow } from "@/lib/supabase/types";
import type { DeviceProductivityBreakdown } from "@/lib/dashboard/aggregate";
import { deriveDeviceStatus } from "@/lib/dashboard/deviceStatus";
import {
  deviceProductivityPercent,
  productivityTier,
  PRODUCTIVITY_TIER_COLOR,
  GOOD_TIER_THRESHOLD,
} from "@/lib/dashboard/productivity";
import { describeHalfArc, describeGoalTick } from "@/lib/dashboard/gauge";
import { formatMinutesAsHours } from "@/lib/dashboard/workPolicy";

const CX = 120;
const CY = 118;
const R = 100;
const STROKE = 18;
const GOAL_FRACTION = GOOD_TIER_THRESHOLD / 100;

/**
 * Resumo da equipe: gauge em meia-rosca com a média de produtividade (mesma fórmula do
 * card por pessoa, ver lib/dashboard/productivity.ts), ao lado de 3 indicadores rápidos
 * (ativos/ociosos agora, horas produtivas no período). Cores e geometria reaproveitam os
 * tokens do dataviz deste dashboard, para manter suporte a tema claro/escuro.
 */
export function TeamProductivityOverview({
  devices,
  breakdown,
  targetSecondsPerDevice,
  now,
  periodLabel,
}: {
  devices: DeviceRow[];
  breakdown: DeviceProductivityBreakdown[];
  targetSecondsPerDevice: number;
  now: Date;
  periodLabel: string;
}) {
  const percents = breakdown.map((b) => deviceProductivityPercent(b, targetSecondsPerDevice));
  const present = percents.filter((p): p is number => p !== null);
  const avg = present.length > 0 ? Math.round(present.reduce((sum, p) => sum + p, 0) / present.length) : null;
  const avgTier = productivityTier(avg ?? 0);
  const avgColor = PRODUCTIVITY_TIER_COLOR[avgTier];

  const online = devices.filter((d) => deriveDeviceStatus(d, now) === "online").length;
  const away = devices.filter((d) => deriveDeviceStatus(d, now) === "away").length;

  const productiveSeconds = breakdown.reduce((sum, b) => sum + b.productiveSeconds, 0);
  const targetSecondsPresent = targetSecondsPerDevice * present.length;

  return (
    <section aria-label="Resumo da equipe" className="viz-root flex flex-wrap gap-4">
      <div
        className="flex flex-1 items-center justify-center rounded-lg p-4"
        style={{ background: "var(--viz-surface)", border: "1px solid var(--viz-gridline)", minWidth: 280 }}
      >
        <div className="relative shrink-0" style={{ width: 240, height: 136 }}>
          <svg width={240} height={136} aria-hidden="true">
            <path
              d={describeHalfArc(CX, CY, R, 1)}
              stroke="var(--viz-gridline)"
              fill="none"
              style={{ strokeWidth: STROKE, strokeLinecap: "round" }}
            />
            {avg !== null && (
              <path
                d={describeHalfArc(CX, CY, R, avg / 100)}
                stroke={avgColor}
                fill="none"
                style={{ strokeWidth: STROKE, strokeLinecap: "round" }}
              />
            )}
            <path
              d={describeGoalTick(CX, CY, R, GOAL_FRACTION)}
              stroke="var(--viz-text-primary)"
              fill="none"
              style={{ strokeWidth: 2.5 }}
            />
          </svg>
          <div className="absolute inset-x-0 bottom-1.5 text-center">
            <div className="font-mono text-4xl leading-none font-semibold" style={{ color: "var(--viz-text-primary)" }}>
              {avg !== null ? `${avg}%` : "—"}
            </div>
            <div
              className="mt-1.5 text-xs font-bold uppercase"
              style={{ color: "var(--viz-text-muted)", letterSpacing: "0.08em" }}
            >
              Média da equipe
            </div>
          </div>
        </div>
      </div>

      <StatCard label="Ativos agora" value={String(online)} hint={`de ${devices.length} dispositivo(s)`} />
      <StatCard label="Ociosos agora" value={String(away)} hint="sem atividade recente" color="var(--viz-status-warning)" />
      <StatCard
        label="Horas produtivas"
        value={formatMinutesAsHours(Math.floor(productiveSeconds / 60))}
        hint={`${periodLabel.toLowerCase()} · meta de ${formatMinutesAsHours(Math.floor(targetSecondsPresent / 60))}`}
      />
    </section>
  );
}

function StatCard({ label, value, hint, color }: { label: string; value: string; hint: string; color?: string }) {
  return (
    <div
      className="flex flex-1 flex-col justify-between gap-2 rounded-lg p-4"
      style={{ background: "var(--viz-surface)", border: "1px solid var(--viz-gridline)", minWidth: 160 }}
    >
      <span className="text-sm font-semibold" style={{ color: "var(--viz-text-secondary)" }}>
        {label}
      </span>
      <span className="font-mono text-4xl font-semibold" style={{ color: color ?? "var(--viz-text-primary)" }}>
        {value}
      </span>
      <span className="text-sm" style={{ color: "var(--viz-text-muted)" }}>
        {hint}
      </span>
    </div>
  );
}
