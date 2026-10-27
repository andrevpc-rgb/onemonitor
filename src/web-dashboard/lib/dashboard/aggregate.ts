import type { DeviceRow, UsageEventRow } from "@/lib/supabase/types";

export interface AggregatedUsage {
  label: string;
  seconds: number;
}

/** Tempo ativo por aplicativo (só eventos Active; Idle/Unclassified não contam como uso). */
export function aggregateByApplication(events: UsageEventRow[]): AggregatedUsage[] {
  return aggregate(
    events.filter((e) => e.activity_type === "Active"),
    (e) => e.application_name || e.process_name,
  );
}

/** Tempo ativo por categoria configurável; sem categoria resolvida cai em "Outros". */
export function aggregateByCategory(events: UsageEventRow[]): AggregatedUsage[] {
  return aggregate(
    events.filter((e) => e.activity_type === "Active"),
    (e) => e.category_name || "Outros",
  );
}

function aggregate(events: UsageEventRow[], keyOf: (e: UsageEventRow) => string): AggregatedUsage[] {
  const totals = new Map<string, number>();
  for (const event of events) {
    const key = keyOf(event);
    totals.set(key, (totals.get(key) ?? 0) + event.duration_seconds);
  }
  return Array.from(totals.entries())
    .map(([label, seconds]) => ({ label, seconds }))
    .sort((a, b) => b.seconds - a.seconds);
}

export interface DeviceProductivity {
  deviceId: string;
  label: string;
  activeSeconds: number;
  idleSeconds: number;
}

/**
 * Ativo vs. inativo por dispositivo — "Não classificado" fica de fora, mesma convenção de
 * aggregateByApplication/aggregateByCategory (não é tempo de uso nem de ausência).
 * Inclui dispositivos sem nenhum evento no período (zerados), para a lista bater com o
 * grid de status.
 */
export function aggregateActiveVsIdleByDevice(
  events: UsageEventRow[],
  devices: Pick<DeviceRow, "device_id" | "hostname">[],
): DeviceProductivity[] {
  const totals = new Map<string, { activeSeconds: number; idleSeconds: number }>();
  for (const device of devices) {
    totals.set(device.device_id, { activeSeconds: 0, idleSeconds: 0 });
  }

  for (const event of events) {
    if (event.activity_type !== "Active" && event.activity_type !== "Idle") {
      continue;
    }
    const entry = totals.get(event.device_id) ?? { activeSeconds: 0, idleSeconds: 0 };
    if (event.activity_type === "Active") {
      entry.activeSeconds += event.duration_seconds;
    } else {
      entry.idleSeconds += event.duration_seconds;
    }
    totals.set(event.device_id, entry);
  }

  return devices
    .map((device) => ({
      deviceId: device.device_id,
      label: device.hostname || device.device_id,
      ...(totals.get(device.device_id) ?? { activeSeconds: 0, idleSeconds: 0 }),
    }))
    .sort((a, b) => b.activeSeconds + b.idleSeconds - (a.activeSeconds + a.idleSeconds));
}

export interface JustifiedAbsence {
  windowTitle: string;
  justification: string;
  seconds: number;
  startedAt: string;
}

/** Períodos de inatividade com motivo informado (ver pop-up de justificativa do agente). */
export function justifiedAbsences(events: UsageEventRow[]): JustifiedAbsence[] {
  return events
    .filter((e): e is UsageEventRow & { justification: string } => e.activity_type === "Idle" && !!e.justification)
    .map((e) => ({
      windowTitle: e.window_title,
      justification: e.justification,
      seconds: e.duration_seconds,
      startedAt: e.started_at,
    }))
    .sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime());
}
