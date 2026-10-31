import type { OrganizationSettingsRow } from "@/lib/supabase/types";

/** Usado quando a organização ainda não tem uma linha em organization_settings (não deveria
 * acontecer após a migração da Fase 8 — ver backfill em docs/supabase_schema.sql — mas evita
 * que a tela de configurações ou o dashboard quebrem se isso ocorrer mesmo assim). */
export const DEFAULT_ORGANIZATION_SETTINGS: Omit<OrganizationSettingsRow, "organization_id" | "updated_at"> = {
  work_start_time: "08:00:00",
  work_end_time: "18:00:00",
  mandatory_break_minutes: 60,
  variable_break_minutes: 0,
  enable_idle_popup: true,
  popup_idle_threshold_minutes: 15,
  usage_events_retention_days: 180,
  workload_alert_enabled: false,
  workload_alert_daily_hours_threshold: 10,
  workload_alert_consecutive_days_threshold: 3,
};

function timeToMinutes(time: string): number {
  const [hours, minutes] = time.split(":").map(Number);
  return hours * 60 + minutes;
}

/** Jornada líquida alvo = (fim - início) - pausa obrigatória - pausas variáveis, em minutos. */
export function calculateNetTargetMinutes(
  settings: Pick<OrganizationSettingsRow, "work_start_time" | "work_end_time" | "mandatory_break_minutes" | "variable_break_minutes">,
): number {
  const grossMinutes = timeToMinutes(settings.work_end_time) - timeToMinutes(settings.work_start_time);
  const netMinutes = grossMinutes - settings.mandatory_break_minutes - settings.variable_break_minutes;
  return Math.max(0, netMinutes);
}

export function formatMinutesAsHours(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return minutes === 0 ? `${hours}h` : `${hours}h${String(minutes).padStart(2, "0")}`;
}
