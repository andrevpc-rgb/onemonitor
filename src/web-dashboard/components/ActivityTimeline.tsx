import type { DeviceRow, UsageEventRow } from "@/lib/supabase/types";
import { formatDate, formatDateTime, formatDuration } from "@/lib/dashboard/format";
import { EmptyState } from "./EmptyState";

const DAY_MS = 24 * 60 * 60 * 1000;
const HOUR_TICKS = [0, 4, 8, 12, 16, 20, 24];

const TYPE_COLOR: Record<UsageEventRow["activity_type"], string> = {
  Active: "var(--viz-series-1)",
  Idle: "var(--viz-series-2)",
  Unclassified: "var(--viz-baseline)",
};

const TYPE_LABEL: Record<UsageEventRow["activity_type"], string> = {
  Active: "Ativo",
  Idle: "Inativo",
  Unclassified: "Não classificado",
};

interface TimelineBlock {
  key: string;
  leftPct: number;
  widthPct: number;
  color: string;
  title: string;
}

/**
 * Gantt de um dia (00h-24h) por dispositivo, pensado como small multiples (um track por
 * dispositivo) em vez de um gráfico só — a posição horizontal É a hora do dia, não há
 * "barra" crescendo de uma baseline (ver references/choosing-a-form.md do skill de
 * dataviz: isso não é bar chart, é timeline).
 */
export function ActivityTimeline({
  devices,
  events,
  dayStartIso,
}: {
  devices: Pick<DeviceRow, "device_id" | "hostname">[];
  events: UsageEventRow[];
  dayStartIso: string;
}) {
  const dayStart = new Date(dayStartIso).getTime();
  const dayEnd = dayStart + DAY_MS;

  const eventsByDevice = new Map<string, UsageEventRow[]>();
  for (const event of events) {
    const list = eventsByDevice.get(event.device_id) ?? [];
    list.push(event);
    eventsByDevice.set(event.device_id, list);
  }

  const usedTypes = new Set(events.map((e) => e.activity_type));

  return (
    <section className="viz-root rounded-lg p-4" style={{ background: "var(--viz-surface)" }}>
      <h2 className="mb-4 text-sm font-semibold" style={{ color: "var(--viz-text-primary)" }}>
        Linha do tempo — {formatDate(dayStartIso)}
      </h2>

      {devices.length === 0 ? (
        <EmptyState message="Nenhum dispositivo para exibir." />
      ) : (
        <>
          <div className="space-y-2">
            {devices.map((device) => {
              const blocks = toBlocks(eventsByDevice.get(device.device_id) ?? [], dayStart, dayEnd);
              return (
                <div key={device.device_id} className="flex items-center gap-3">
                  <span
                    className="w-28 shrink-0 truncate text-xs"
                    style={{ color: "var(--viz-text-secondary)" }}
                    title={device.hostname ?? device.device_id}
                  >
                    {device.hostname || device.device_id}
                  </span>
                  <div
                    className="relative h-5 flex-1 overflow-hidden rounded-sm"
                    style={{ background: "var(--viz-gridline)" }}
                  >
                    {blocks.map((block) => (
                      <div
                        key={block.key}
                        title={block.title}
                        className="absolute inset-y-0"
                        style={{
                          left: `${block.leftPct}%`,
                          width: `${block.widthPct}%`,
                          background: block.color,
                          boxShadow: "0 0 0 1px var(--viz-surface)",
                        }}
                      />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>

          <div
            className="mt-1 flex text-[10px] tabular-nums"
            style={{ color: "var(--viz-text-muted)" }}
          >
            <span className="w-28 shrink-0" aria-hidden />
            <div className="relative flex-1">
              {HOUR_TICKS.map((hour) => (
                <span
                  key={hour}
                  className="absolute -translate-x-1/2"
                  style={{ left: `${(hour / 24) * 100}%` }}
                >
                  {String(hour).padStart(2, "0")}h
                </span>
              ))}
            </div>
          </div>

          <ul className="mt-5 flex flex-wrap gap-x-4 gap-y-2">
            {(["Active", "Idle", "Unclassified"] as const)
              .filter((type) => usedTypes.has(type))
              .map((type) => (
                <li key={type} className="flex items-center gap-2 text-xs" style={{ color: "var(--viz-text-secondary)" }}>
                  <span
                    className="inline-block h-2.5 w-2.5 rounded-full"
                    style={{ background: TYPE_COLOR[type] }}
                    aria-hidden
                  />
                  <span>{TYPE_LABEL[type]}</span>
                </li>
              ))}
          </ul>
        </>
      )}
    </section>
  );
}

function toBlocks(events: UsageEventRow[], dayStart: number, dayEnd: number): TimelineBlock[] {
  const dayMs = dayEnd - dayStart;

  return events
    .map((event) => {
      const start = Math.max(new Date(event.started_at).getTime(), dayStart);
      const end = Math.min(new Date(event.ended_at).getTime(), dayEnd);
      if (end <= start) {
        return null;
      }

      return {
        key: event.id,
        leftPct: ((start - dayStart) / dayMs) * 100,
        widthPct: Math.max(0.2, ((end - start) / dayMs) * 100),
        color: TYPE_COLOR[event.activity_type],
        title: `${TYPE_LABEL[event.activity_type]} · ${formatDateTime(event.started_at)}–${formatDateTime(event.ended_at)} · ${formatDuration(event.duration_seconds)}`,
      } satisfies TimelineBlock;
    })
    .filter((block): block is TimelineBlock => block !== null);
}
