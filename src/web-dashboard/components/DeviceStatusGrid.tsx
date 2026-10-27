import { Wifi, Clock, WifiOff } from "lucide-react";
import type { DeviceRow } from "@/lib/supabase/types";
import { deriveDeviceStatus, DEVICE_STATUS_LABELS, type DeviceStatusLabel } from "@/lib/dashboard/deviceStatus";
import { formatRelativeTime } from "@/lib/dashboard/format";
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

export function DeviceStatusGrid({ devices, now }: { devices: DeviceRow[]; now: Date }) {
  if (devices.length === 0) {
    return (
      <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
        <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
          Dispositivos
        </h2>
        <EmptyState message="Nenhum dispositivo sincronizou dados ainda." />
      </section>
    );
  }

  const sorted = [...devices].sort((a, b) => {
    const statusDiff = STATUS_ORDER[deriveDeviceStatus(a, now)] - STATUS_ORDER[deriveDeviceStatus(b, now)];
    if (statusDiff !== 0) {
      return statusDiff;
    }
    return new Date(b.last_seen_at).getTime() - new Date(a.last_seen_at).getTime();
  });

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Dispositivos
      </h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {sorted.map((device) => {
          const status = deriveDeviceStatus(device, now);
          const Icon = STATUS_ICON[status];
          const color = STATUS_COLOR[status];

          return (
            <div
              key={device.id}
              className="rounded-md p-3"
              style={{ border: "1px solid var(--viz-gridline)" }}
            >
              <p
                className="truncate text-sm font-medium"
                style={{ color: "var(--viz-text-primary)" }}
                title={device.hostname ?? device.device_id}
              >
                {device.hostname || device.device_id}
              </p>
              <div className="mt-2 flex items-center gap-1.5 text-xs" style={{ color }}>
                <Icon size={14} aria-hidden />
                <span className="font-medium">{DEVICE_STATUS_LABELS[status]}</span>
              </div>
              <p className="mt-1 text-xs" style={{ color: "var(--viz-text-muted)" }}>
                Visto {formatRelativeTime(device.last_seen_at, now)}
              </p>
            </div>
          );
        })}
      </div>
    </section>
  );
}
