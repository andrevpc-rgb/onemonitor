import { SlidersHorizontal } from "lucide-react";
import { RANGE_OPTIONS, type RangeKey } from "@/lib/dashboard/dateRange";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import type { DeviceRow } from "@/lib/supabase/types";

/**
 * Form GET puro (sem "use client"): o próprio navegador refaz a navegação para
 * "/?range=...&device=..." ao enviar — o Server Component relê searchParams e tudo abaixo
 * se refaz com a mesma fatia de dados (ver skill de dataviz: "filtros escopam tudo abaixo
 * deles, uma linha só, acima do conteúdo").
 */
export function DashboardFilters({
  devices,
  selectedRange,
  selectedDevice,
}: {
  devices: Pick<DeviceRow, "device_id" | "hostname" | "device_name">[];
  selectedRange: RangeKey;
  selectedDevice: string | undefined;
}) {
  return (
    <form
      method="GET"
      className="viz-root flex flex-wrap items-center gap-3 rounded-lg p-3"
      style={{ background: "var(--viz-surface)" }}
    >
      <SlidersHorizontal size={16} style={{ color: "var(--viz-text-muted)" }} aria-hidden />

      <select
        name="range"
        defaultValue={selectedRange}
        className="rounded-md border px-2 py-1 text-sm"
        style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-primary)", background: "var(--viz-surface)" }}
      >
        {RANGE_OPTIONS.map((option) => (
          <option key={option.key} value={option.key}>
            {option.label}
          </option>
        ))}
      </select>

      <select
        name="device"
        defaultValue={selectedDevice ?? "all"}
        className="rounded-md border px-2 py-1 text-sm"
        style={{ borderColor: "var(--viz-gridline)", color: "var(--viz-text-primary)", background: "var(--viz-surface)" }}
      >
        <option value="all">Todos os dispositivos</option>
        {devices.map((device) => (
          <option key={device.device_id} value={device.device_id}>
            {displayDeviceName(device)}
          </option>
        ))}
      </select>

      <button
        type="submit"
        className="rounded-md px-3 py-1 text-sm font-medium text-white"
        style={{ background: "var(--viz-text-primary)" }}
      >
        Aplicar
      </button>
    </form>
  );
}
