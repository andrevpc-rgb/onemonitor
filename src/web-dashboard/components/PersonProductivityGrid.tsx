"use client";

import { useMemo, useState } from "react";
import { Wifi, Clock, WifiOff, Search } from "lucide-react";
import type { DeviceRow } from "@/lib/supabase/types";
import type { DeviceProductivityBreakdown } from "@/lib/dashboard/aggregate";
import { deriveDeviceStatus, DEVICE_STATUS_LABELS, type DeviceStatusLabel } from "@/lib/dashboard/deviceStatus";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import { formatRelativeTime, formatDate, formatDateTime, formatDuration } from "@/lib/dashboard/format";
import {
  deviceProductivityPercent,
  productivityTier,
  PRODUCTIVITY_TIER_COLOR,
  PRODUCTIVITY_TIER_LABELS,
  GOOD_TIER_THRESHOLD,
} from "@/lib/dashboard/productivity";
import { PRODUCTIVITY_CLASS_COLOR } from "@/lib/dashboard/productivityClassification";
import { describeHalfArc, describeGoalTick } from "@/lib/dashboard/gauge";
import { assignCategoryColors } from "@/lib/viz/categoryColors";
import { EmptyState } from "./EmptyState";

const STATUS_ICON: Record<DeviceStatusLabel, typeof Wifi> = { online: Wifi, away: Clock, offline: WifiOff };

// Cor do status nunca sozinha (sempre ícone + rótulo também) — ver skill de dataviz.
const STATUS_COLOR: Record<DeviceStatusLabel, string> = {
  online: "var(--viz-status-good)",
  away: "var(--viz-status-warning)",
  offline: "var(--viz-text-muted)",
};

const STATUS_ORDER: Record<DeviceStatusLabel, number> = { online: 0, away: 1, offline: 2 };

const CX = 100;
const CY = 100;
const R = 84;
const STROKE = 14;
const GOAL_FRACTION = GOOD_TIER_THRESHOLD / 100;

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "?";
  }
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function describeNow(
  status: DeviceStatusLabel,
  device: DeviceRow,
  latest: DeviceProductivityBreakdown["latestActiveEvent"],
): string {
  if (status === "online") {
    if (!latest) {
      return "Sem atividade produtiva registrada no período";
    }
    const title = latest.windowTitle && latest.windowTitle !== latest.applicationName ? ` · ${latest.windowTitle}` : "";
    return `Agora: ${latest.applicationName}${title}`;
  }
  if (status === "away") {
    return `Sem atividade desde ${formatDateTime(device.last_seen_at)}`;
  }
  return `Desconectou às ${formatDateTime(device.last_seen_at)}`;
}

interface PersonProductivityGridProps {
  devices: DeviceRow[];
  now: Date;
  breakdown: DeviceProductivityBreakdown[];
  targetSecondsPerDevice: number;
}

/**
 * Grid de produtividade por dispositivo/colaborador — meia-rosca com tracinho na meta,
 * barra empilhada Produtivo/Neutro/Improdutivo e status em tempo real. "use client" pelos
 * filtros locais (busca, setor, ordenação); os dados já foram buscados pelo Server Component
 * pai (page.tsx), nada aqui pede dado novo ao servidor.
 */
export function PersonProductivityGrid({ devices, now, breakdown, targetSecondsPerDevice }: PersonProductivityGridProps) {
  const [query, setQuery] = useState("");
  const [sector, setSector] = useState<string>("todos");
  const [asc, setAsc] = useState(false);

  const breakdownByDevice = useMemo(() => new Map(breakdown.map((b) => [b.deviceId, b])), [breakdown]);

  const sectors = useMemo(() => {
    const seen = new Set<string>();
    for (const d of devices) {
      if (d.sector) {
        seen.add(d.sector);
      }
    }
    return Array.from(seen);
  }, [devices]);
  const sectorColors = useMemo(() => assignCategoryColors(sectors), [sectors]);

  const cards = useMemo(
    () =>
      devices.map((device) => {
        const b =
          breakdownByDevice.get(device.device_id) ?? {
            deviceId: device.device_id,
            label: displayDeviceName(device),
            sector: device.sector,
            productiveSeconds: 0,
            neutralSeconds: 0,
            unproductiveSeconds: 0,
            latestActiveEvent: null,
          };
        const percent = deviceProductivityPercent(b, targetSecondsPerDevice);
        return { device, breakdown: b, percent };
      }),
    [devices, breakdownByDevice, targetSecondsPerDevice],
  );

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return cards.filter((c) => {
      if (sector !== "todos" && c.device.sector !== sector) {
        return false;
      }
      if (needle && !displayDeviceName(c.device).toLowerCase().includes(needle)) {
        return false;
      }
      return true;
    });
  }, [cards, sector, query]);

  const sorted = useMemo(
    () =>
      [...filtered].sort((a, b) => {
        const statusDiff = STATUS_ORDER[deriveDeviceStatus(a.device, now)] - STATUS_ORDER[deriveDeviceStatus(b.device, now)];
        if (a.percent === null && b.percent === null) {
          return statusDiff;
        }
        if (a.percent === null) {
          return 1;
        }
        if (b.percent === null) {
          return -1;
        }
        return asc ? a.percent - b.percent : b.percent - a.percent;
      }),
    [filtered, asc, now],
  );

  return (
    <section className="viz-root space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <nav aria-label="Filtrar por setor" className="flex flex-wrap gap-2">
          <SectorChip label="Todos" active={sector === "todos"} onClick={() => setSector("todos")} />
          {sectors.map((s) => (
            <SectorChip
              key={s}
              label={s}
              active={sector === s}
              color={sectorColors.get(s)}
              onClick={() => setSector(s)}
            />
          ))}
        </nav>

        <div className="flex items-center gap-2">
          <div className="relative">
            <Search
              size={14}
              className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2"
              style={{ color: "var(--viz-text-muted)" }}
              aria-hidden
            />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Buscar..."
              className="rounded-md border py-1.5 pr-3 pl-8 text-sm"
              style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-primary)", background: "var(--viz-surface)" }}
            />
          </div>
          <button
            type="button"
            onClick={() => setAsc((v) => !v)}
            className="rounded-md border px-3 py-1.5 text-sm font-medium"
            style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-primary)", background: "var(--viz-surface)" }}
          >
            {asc ? "Menor % primeiro" : "Maior % primeiro"}
          </button>
        </div>
      </div>

      {devices.length === 0 ? (
        <EmptyState message="Nenhum dispositivo sincronizou dados ainda." />
      ) : sorted.length === 0 ? (
        <EmptyState message="Nenhum dispositivo encontrado para esse filtro." />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {sorted.map(({ device, breakdown: b, percent }) => {
            const status = deriveDeviceStatus(device, now);
            const StatusIcon = STATUS_ICON[status];
            const tier = productivityTier(percent ?? 0);
            const tierColor = PRODUCTIVITY_TIER_COLOR[tier];
            const avatarColor = device.sector ? sectorColors.get(device.sector) ?? "var(--viz-text-muted)" : "var(--viz-text-muted)";

            const total = targetSecondsPerDevice > 0 ? targetSecondsPerDevice : b.productiveSeconds + b.neutralSeconds + b.unproductiveSeconds;
            const prodW = total > 0 ? Math.min(100, (b.productiveSeconds / total) * 100) : 0;
            const neuW = total > 0 ? Math.min(100 - prodW, (b.neutralSeconds / total) * 100) : 0;
            const impW = total > 0 ? Math.min(100 - prodW - neuW, (b.unproductiveSeconds / total) * 100) : 0;

            return (
              <article
                key={device.id}
                className="flex flex-col gap-3.5 rounded-lg p-4"
                style={{ background: "var(--viz-surface)", border: "1px solid var(--viz-gridline)" }}
              >
                <div className="flex items-center gap-3">
                  <div
                    className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-bold text-white"
                    style={{ background: avatarColor }}
                    aria-hidden
                  >
                    {initialsOf(displayDeviceName(device))}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-bold" style={{ color: "var(--viz-text-primary)" }} title={displayDeviceName(device)}>
                      {displayDeviceName(device)}
                    </p>
                    <p className="truncate text-xs" style={{ color: "var(--viz-text-muted)" }}>
                      {device.sector ?? "Sem setor"}
                    </p>
                  </div>
                  <span
                    className="inline-flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold whitespace-nowrap"
                    style={{ color: STATUS_COLOR[status], background: "var(--viz-gridline)" }}
                  >
                    <StatusIcon size={12} aria-hidden />
                    {DEVICE_STATUS_LABELS[status]}
                  </span>
                </div>

                <div className="relative mx-auto" style={{ width: 200, height: 112 }}>
                  <svg
                    width={200}
                    height={112}
                    role="img"
                    aria-label={percent !== null ? `${displayDeviceName(device)}: ${percent.toFixed(0)}% de produtividade` : `${displayDeviceName(device)}: sem dados`}
                  >
                    <path d={describeHalfArc(CX, CY, R, 1)} stroke="var(--viz-gridline)" fill="none" style={{ strokeWidth: STROKE, strokeLinecap: "round" }} />
                    {percent !== null && (
                      <path d={describeHalfArc(CX, CY, R, percent / 100)} stroke={tierColor} fill="none" style={{ strokeWidth: STROKE, strokeLinecap: "round" }} />
                    )}
                    <path d={describeGoalTick(CX, CY, R, GOAL_FRACTION)} stroke="var(--viz-text-primary)" fill="none" style={{ strokeWidth: 2 }} />
                  </svg>
                  <div className="absolute inset-x-0 bottom-1 text-center">
                    <div className="font-mono text-3xl leading-none font-semibold" style={{ color: "var(--viz-text-primary)" }}>
                      {percent !== null ? `${percent.toFixed(0)}%` : "—"}
                    </div>
                    <div className="mt-1 text-xs font-extrabold uppercase" style={{ color: percent !== null ? tierColor : "var(--viz-text-muted)", letterSpacing: "0.07em" }}>
                      {percent !== null ? PRODUCTIVITY_TIER_LABELS[tier] : "Sem dados"}
                    </div>
                  </div>
                </div>

                <div className="flex flex-col gap-2">
                  <div className="flex h-2 overflow-hidden rounded-full" style={{ background: "var(--viz-gridline)" }}>
                    {prodW > 0 && <span style={{ width: `${prodW}%`, background: PRODUCTIVITY_CLASS_COLOR.productive }} />}
                    {neuW > 0 && <span style={{ width: `${neuW}%`, background: PRODUCTIVITY_CLASS_COLOR.neutral, marginLeft: 1 }} />}
                    {impW > 0 && <span style={{ width: `${impW}%`, background: PRODUCTIVITY_CLASS_COLOR.unproductive, marginLeft: 1 }} />}
                  </div>
                  <div className="flex justify-between gap-1.5 font-mono text-xs" style={{ color: "var(--viz-text-muted)" }}>
                    <span>
                      Prod. <b style={{ color: "var(--viz-text-primary)" }}>{formatDuration(b.productiveSeconds)}</b>
                    </span>
                    <span>
                      Neutro <b style={{ color: "var(--viz-text-primary)" }}>{formatDuration(b.neutralSeconds)}</b>
                    </span>
                    <span>
                      Improd. <b style={{ color: "var(--viz-text-primary)" }}>{formatDuration(b.unproductiveSeconds)}</b>
                    </span>
                  </div>
                </div>

                <p
                  className="truncate border-t pt-2.5 text-xs"
                  style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-muted)" }}
                  title={describeNow(status, device, b.latestActiveEvent)}
                >
                  {describeNow(status, device, b.latestActiveEvent)}
                </p>

                <p className="text-xs" style={{ color: "var(--viz-text-muted)" }}>
                  Visto {formatRelativeTime(device.last_seen_at, now)} · monitorando desde {formatDate(device.first_seen_at)}
                </p>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

function SectorChip({ label, active, color, onClick }: { label: string; active: boolean; color?: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className="rounded-full px-4 py-2 text-sm font-bold"
      style={{
        border: `1px solid ${active ? "var(--viz-text-primary)" : "var(--viz-gridline)"}`,
        background: active ? "var(--viz-text-primary)" : "var(--viz-surface)",
        color: active ? "var(--background)" : "var(--viz-text-primary)",
      }}
    >
      {!active && color && <span className="mr-1.5 inline-block h-2 w-2 rounded-full" style={{ background: color }} aria-hidden />}
      {label}
    </button>
  );
}
