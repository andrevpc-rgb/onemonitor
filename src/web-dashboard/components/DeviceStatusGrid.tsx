"use client";

import { useMemo, useState } from "react";
import { Wifi, Clock, WifiOff, Search } from "lucide-react";
import type { DeviceRow } from "@/lib/supabase/types";
import type { DeviceProductivity } from "@/lib/dashboard/aggregate";
import { deriveDeviceStatus, DEVICE_STATUS_LABELS, type DeviceStatusLabel } from "@/lib/dashboard/deviceStatus";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import { formatRelativeTime, formatDate } from "@/lib/dashboard/format";
import { productivityPercent, productivityTier, PRODUCTIVITY_TIER_COLOR } from "@/lib/dashboard/productivity";
import { EmptyState } from "./EmptyState";

const STATUS_ICON: Record<DeviceStatusLabel, typeof Wifi> = {
  online: Wifi,
  away: Clock,
  offline: WifiOff,
};

// Cor do status nunca sozinha (sempre ícone + rótulo também) — ver skill de dataviz.
// "offline" fica em tinta neutra: um dispositivo parado não é necessariamente um problema.
const STATUS_COLOR: Record<DeviceStatusLabel, string> = {
  online: "var(--viz-status-good)",
  away: "var(--viz-status-warning)",
  offline: "var(--viz-text-muted)",
};

const STATUS_ORDER: Record<DeviceStatusLabel, number> = { online: 0, away: 1, offline: 2 };

interface DeviceStatusGridProps {
  devices: DeviceRow[];
  now: Date;
  /** Ativo/inativo por dispositivo no período selecionado nos filtros (ver page.tsx). */
  productivity: DeviceProductivity[];
  /** Rótulo do período, só para deixar claro de onde vem o % de produtividade do card. */
  periodLabel: string;
}

/**
 * "use client" só pelo campo de busca (filtra localmente, sem recarregar a página — os
 * dispositivos já foram buscados pelo Server Component pai). Tudo mais aqui é só leitura
 * de props, nada pede dado novo ao servidor.
 */
export function DeviceStatusGrid({ devices, now, productivity, periodLabel }: DeviceStatusGridProps) {
  const [query, setQuery] = useState("");

  const productivityByDevice = useMemo(() => new Map(productivity.map((p) => [p.deviceId, p])), [productivity]);

  const sorted = useMemo(
    () =>
      [...devices].sort((a, b) => {
        const statusDiff = STATUS_ORDER[deriveDeviceStatus(a, now)] - STATUS_ORDER[deriveDeviceStatus(b, now)];
        if (statusDiff !== 0) {
          return statusDiff;
        }
        return new Date(b.last_seen_at).getTime() - new Date(a.last_seen_at).getTime();
      }),
    [devices, now],
  );

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) {
      return sorted;
    }
    return sorted.filter((d) => displayDeviceName(d).toLowerCase().includes(needle));
  }, [sorted, query]);

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
          Dispositivos
        </h2>
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
            placeholder="Buscar dispositivo..."
            className="rounded-md border py-1.5 pr-3 pl-8 text-sm"
            style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-primary)", background: "var(--viz-surface)" }}
          />
        </div>
      </div>

      {devices.length === 0 ? (
        <EmptyState message="Nenhum dispositivo sincronizou dados ainda." />
      ) : filtered.length === 0 ? (
        <EmptyState message="Nenhum dispositivo encontrado para essa busca." />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((device) => {
            const status = deriveDeviceStatus(device, now);
            const Icon = STATUS_ICON[status];
            const statusColor = STATUS_COLOR[status];

            const deviceProductivity = productivityByDevice.get(device.device_id);
            const hasProductivityData = !!deviceProductivity && deviceProductivity.activeSeconds + deviceProductivity.idleSeconds > 0;
            const percent = deviceProductivity ? productivityPercent(deviceProductivity) : 0;
            const tier = productivityTier(percent);
            const tierColor = PRODUCTIVITY_TIER_COLOR[tier];

            return (
              <div key={device.id} className="rounded-md p-3" style={{ border: "1px solid var(--viz-gridline)" }}>
                <div className="flex items-start justify-between gap-2">
                  <p
                    className="truncate text-sm font-medium"
                    style={{ color: "var(--viz-text-primary)" }}
                    title={displayDeviceName(device)}
                  >
                    {displayDeviceName(device)}
                  </p>
                  {hasProductivityData && (
                    <span
                      className="shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums"
                      style={{ color: tierColor, background: "var(--viz-gridline)" }}
                      title={`Produtividade (${periodLabel.toLowerCase()}): ${percent.toFixed(1)}%`}
                    >
                      {percent.toFixed(0)}%
                    </span>
                  )}
                </div>

                <div className="mt-2 flex items-center gap-1.5 text-xs" style={{ color: statusColor }}>
                  <Icon size={14} aria-hidden />
                  <span className="font-medium">{DEVICE_STATUS_LABELS[status]}</span>
                </div>

                <p className="mt-1 text-xs" style={{ color: "var(--viz-text-muted)" }}>
                  Visto {formatRelativeTime(device.last_seen_at, now)} · monitorando desde {formatDate(device.first_seen_at)}
                </p>

                {hasProductivityData && (
                  <div className="mt-2.5 h-1.5 rounded-full" style={{ background: "var(--viz-gridline)" }}>
                    <div
                      className="h-1.5 rounded-full"
                      style={{ width: `${Math.min(100, percent)}%`, background: tierColor }}
                    />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
