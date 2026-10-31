import type { DeviceRow, UsageEventRow } from "@/lib/supabase/types";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import { classifyProductivity, type ProductivityClass, type CustomProductivityRule } from "@/lib/dashboard/productivityClassification";

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
export function aggregateByApplication(
  events: UsageEventRow[],
  customRules: CustomProductivityRule[] = [],
): AggregatedUsage[] {
  const activeEvents = events.filter((e) => e.activity_type === "Active");

  const totals = new Map<string, number>();
  const classTotals = new Map<string, Record<ProductivityClass, number>>();

  for (const event of activeEvents) {
    const key = event.application_name || event.process_name;
    totals.set(key, (totals.get(key) ?? 0) + event.duration_seconds);

    const productivityClass = classifyProductivity(key, event.window_title, customRules);
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
export function aggregateByProductivityClass(
  events: UsageEventRow[],
  customRules: CustomProductivityRule[] = [],
): ProductivityClassTotals {
  const totals: ProductivityClassTotals = { productiveSeconds: 0, neutralSeconds: 0, unproductiveSeconds: 0 };

  for (const event of events) {
    if (event.activity_type !== "Active") {
      continue;
    }
    const key = event.application_name || event.process_name;
    const productivityClass = classifyProductivity(key, event.window_title, customRules);
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

export interface DeviceProductivityBreakdown {
  deviceId: string;
  label: string;
  sector: string | null;
  productiveSeconds: number;
  neutralSeconds: number;
  unproductiveSeconds: number;
  /** Evento Active mais recente do dispositivo no período — para o "Agora: ..." do card. */
  latestActiveEvent: { applicationName: string; windowTitle: string; startedAt: string } | null;
}

/**
 * Produtivo/Neutro/Improdutivo por dispositivo (mesma classificação do Gauge da
 * organização, ver aggregateByProductivityClass, só que quebrado por dispositivo — para o
 * grid de cards por colaborador). Inclui dispositivos sem nenhum evento no período
 * (zerados), para a lista bater com o grid de status.
 */
export function aggregateProductivityByDevice(
  events: UsageEventRow[],
  devices: Pick<DeviceRow, "device_id" | "hostname" | "device_name" | "sector">[],
  customRules: CustomProductivityRule[] = [],
): DeviceProductivityBreakdown[] {
  const totals = new Map<string, ProductivityClassTotals>();
  const latestActive = new Map<string, UsageEventRow>();

  for (const device of devices) {
    totals.set(device.device_id, { productiveSeconds: 0, neutralSeconds: 0, unproductiveSeconds: 0 });
  }

  for (const event of events) {
    if (event.activity_type !== "Active") {
      continue;
    }

    const entry = totals.get(event.device_id) ?? { productiveSeconds: 0, neutralSeconds: 0, unproductiveSeconds: 0 };
    const key = event.application_name || event.process_name;
    const productivityClass = classifyProductivity(key, event.window_title, customRules);
    if (productivityClass === "productive") {
      entry.productiveSeconds += event.duration_seconds;
    } else if (productivityClass === "neutral") {
      entry.neutralSeconds += event.duration_seconds;
    } else {
      entry.unproductiveSeconds += event.duration_seconds;
    }
    totals.set(event.device_id, entry);

    const current = latestActive.get(event.device_id);
    if (!current || event.started_at > current.started_at) {
      latestActive.set(event.device_id, event);
    }
  }

  return devices.map((device) => {
    const latest = latestActive.get(device.device_id);
    return {
      deviceId: device.device_id,
      label: displayDeviceName(device),
      sector: device.sector,
      ...(totals.get(device.device_id) ?? { productiveSeconds: 0, neutralSeconds: 0, unproductiveSeconds: 0 }),
      latestActiveEvent: latest
        ? { applicationName: latest.application_name, windowTitle: latest.window_title, startedAt: latest.started_at }
        : null,
    };
  });
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
