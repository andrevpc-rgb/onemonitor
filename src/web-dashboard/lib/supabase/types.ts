/** Espelho das tabelas de docs/supabase_schema.sql. Mantido à mão (sem geração automática). */

export interface DeviceRow {
  id: string;
  device_id: string;
  hostname: string | null;
  first_seen_at: string;
  last_seen_at: string;
}

export interface UsageEventRow {
  id: string;
  event_id: string;
  device_id: string;
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
