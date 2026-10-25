import type { UsageEventRow } from "@/lib/supabase/types";

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
