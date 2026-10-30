/** Espelho das tabelas de docs/supabase_schema.sql. Mantido à mão (sem geração automática). */

export type DeviceStatus = "online" | "offline" | "stopped";
export type OrganizationRole = "owner" | "manager" | "employee";

export interface OrganizationRow {
  id: string;
  name: string;
  slug: string;
  api_key: string;
  created_at: string;
}

export interface ProfileRow {
  id: string;
  organization_id: string;
  role: OrganizationRole;
  created_at: string;
}

export interface DeviceRow {
  id: string;
  device_id: string;
  organization_id: string | null;
  /** Relatado pelo agente (Environment.MachineName + UserName), atualizado a cada sync. */
  hostname: string | null;
  /** Rótulo exibido no dashboard — editável em /devices; ver lib/dashboard/deviceDisplay.ts. */
  device_name: string | null;
  first_seen_at: string;
  last_seen_at: string;
  status: DeviceStatus;
  revoked_at: string | null;
}

export interface UsageEventRow {
  id: string;
  event_id: string;
  device_id: string;
  organization_id: string | null;
  started_at: string;
  ended_at: string;
  duration_seconds: number;
  activity_type: "Active" | "Idle" | "Unclassified";
  application_name: string;
  process_name: string;
  window_title: string;
  category_name: string | null;
  justification: string | null;
  created_at: string;
  updated_at: string;
}

export type UsageEventInsert = Omit<
  UsageEventRow,
  "id" | "duration_seconds" | "created_at" | "updated_at"
>;

/** work_start_time/work_end_time chegam como "HH:MM:SS" (coluna `time` do Postgres). */
export interface OrganizationSettingsRow {
  organization_id: string;
  work_start_time: string;
  work_end_time: string;
  mandatory_break_minutes: number;
  variable_break_minutes: number;
  enable_idle_popup: boolean;
  popup_idle_threshold_minutes: number;
  usage_events_retention_days: number;
  updated_at: string;
}

export type OrganizationSettingsUpdate = Omit<OrganizationSettingsRow, "organization_id" | "updated_at">;

export interface AuditLogRow {
  id: string;
  organization_id: string;
  actor_id: string | null;
  action: string;
  target_type: string;
  target_id: string | null;
  changes: Record<string, unknown> | null;
  created_at: string;
}

export interface ProductivityRuleRow {
  id: string;
  organization_id: string;
  keyword: string;
  classification: "productive" | "neutral" | "unproductive";
  created_at: string;
}
