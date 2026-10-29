import type { DeviceRow, UsageEventRow } from "@/lib/supabase/types";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import { classifyProductivity, type ProductivityClass } from "@/lib/dashboard/productivityClassification";

export interface AggregatedUsage {
  label: string;
  seconds: number;
  /** Classe dominante (mais segundos) entre os eventos deste app no período — ver aggregateByApplication. */
  productivityClass?: ProductivityClass;
}

/**
 * Tempo ativo por aplicativo (só eventos Active; Idle/Unclassified não contam como uso),
 * com a classe de produtividade dominante de cada app (app + título da janela de cada
 * evento, não só o nome do processo — assim "chrome.exe" com e-CAC e YouTube misturados
 * ainda classifica cada evento corretamente antes de somar).
 */
export function aggregateByApplication(events: UsageEventRow[]): AggregatedUsage[] {
  const activeEvents = events.filter((e) => e.activity_type === "Active");

  const totals = new Map<string, number>();
  const classTotals = new Map<string, Record<ProductivityClass, number>>();

  for (const event of activeEvents) {
    const key = event.application_name || event.process_name;
    totals.set(key, (totals.get(key) ?? 0) + event.duration_seconds);

    const productivityClass = classifyProductivity(key, event.window_title);
    const buckets = classTotals.get(key) ?? { productive: 0, neutral: 0, unproductive: 0 };
    buckets[productivityClass] += event.duration_seconds;
    classTotals.set(key, buckets);
  }

  return Array.from(totals.entries())
    .map(([label, seconds]) => ({
      label,
      seconds,
      productivityClass: dominantProductivityClass(classTotals.get(label)!),
    }))
    .sort((a, b) => b.seconds - a.seconds);
}

function dominantProductivityClass(buckets: Record<ProductivityClass, number>): ProductivityClass {
  return (Object.entries(buckets) as [ProductivityClass, number][]).sort((a, b) => b[1] - a[1])[0][0];
}

export interface ProductivityClassTotals {
  productiveSeconds: number;
  neutralSeconds: number;
  unproductiveSeconds: number;
}

/** Soma de tempo ativo por classe de produtividade — usado pelo Gauge (ver page.tsx). */
export function aggregateByProductivityClass(events: UsageEventRow[]): ProductivityClassTotals {
  const totals: ProductivityClassTotals = { productiveSeconds: 0, neutralSeconds: 0, unproductiveSeconds: 0 };

  for (const event of events) {
    if (event.activity_type !== "Active") {
      continue;
    }
    const key = event.application_name || event.process_name;
    const productivityClass = classifyProductivity(key, event.window_title);
    if (productivityClass === "productive") {
      totals.productiveSeconds += event.duration_seconds;
    } else if (productivityClass === "neutral") {
      totals.neutralSeconds += event.duration_seconds;
    } else {
      totals.unproductiveSeconds += event.duration_seconds;
    }
  }

  return totals;
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
  devices: Pick<DeviceRow, "device_id" | "hostname" | "device_name">[],
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
      label: displayDeviceName(device),
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
