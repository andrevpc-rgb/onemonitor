import { NextResponse } from "next/server";
import { createSupabaseAdminClient } from "@/lib/supabase/admin";
import { isValidSyncBatch, type DeviceStatusState, type SyncBatch } from "@/lib/sync/contracts";
import type { DeviceStatus, UsageEventInsert } from "@/lib/supabase/types";

/**
 * Ingestão de lotes enviados pelo agente desktop C# (SyncService -> HttpSyncApiClient).
 *
 * Os códigos de status aqui precisam continuar alinhados com a classificação que
 * HttpSyncApiClient já faz do lado do agente: 2xx = sucesso; 4xx = falha permanente (não
 * adianta reenviar o mesmo lote); 5xx = falha transitória (o agente retenta com backoff).
 *
 * Fase 6: a api_key enviada pelo agente (header X-Api-Key, mesmo valor de
 * appsettings.json -> Sync.ApiKey) não é mais comparada a um único segredo global — ela
 * identifica a organização dona do lote (tabela organizations.api_key), que é então
 * gravada em todo device/usage_event que este request ingerir.
 */
export async function POST(request: Request) {
  const apiKey = request.headers.get("x-api-key");

  if (!apiKey) {
    return NextResponse.json({ error: "Header X-Api-Key ausente." }, { status: 401 });
  }

  const supabase = createSupabaseAdminClient();

  const { data: organization, error: organizationError } = await supabase
    .from("organizations")
    .select("id")
    .eq("api_key", apiKey)
    .maybeSingle();

  if (organizationError) {
    return NextResponse.json({ error: organizationError.message }, { status: 500 });
  }

  if (!organization) {
    return NextResponse.json({ error: "API key inválida." }, { status: 401 });
  }

  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return NextResponse.json({ error: "Corpo da requisição não é um JSON válido." }, { status: 400 });
  }

  if (!isValidSyncBatch(payload)) {
    return NextResponse.json({ error: "Formato de SyncBatch inválido." }, { status: 400 });
  }

  const batch: SyncBatch = payload;
  const organizationId = organization.id;

  const deviceUpsert: {
    device_id: string;
    organization_id: string;
    last_seen_at: string;
    status?: DeviceStatus;
  } = {
    device_id: batch.deviceId,
    organization_id: organizationId,
    last_seen_at: new Date().toISOString(),
  };
  if (batch.status) {
    deviceUpsert.status = toDeviceStatus(batch.status.state);
  }

  const { error: deviceError } = await supabase
    .from("devices")
    .upsert(deviceUpsert, { onConflict: "device_id" });

  if (deviceError) {
    return NextResponse.json({ error: deviceError.message }, { status: 500 });
  }

  if (batch.events.length === 0) {
    return NextResponse.json({ received: 0, upserted: 0 });
  }

  const rows: UsageEventInsert[] = batch.events.map((event) => ({
    event_id: event.eventId,
    device_id: batch.deviceId,
    organization_id: organizationId,
    started_at: event.startTime,
    ended_at: event.endTime,
    activity_type: event.type,
    // application_name hoje espelha process_name (ver docs/supabase_schema.sql) — fica
    // pronto para um futuro mapeamento "chrome" -> "Google Chrome" sem mudar o schema.
    application_name: event.processName,
    process_name: event.processName,
    window_title: event.windowTitle,
    category_name: event.category || null,
    justification: event.justification,
  }));

  const { error: eventsError, count } = await supabase
    .from("usage_events")
    .upsert(rows, { onConflict: "event_id", count: "exact" });

  if (eventsError) {
    return NextResponse.json({ error: eventsError.message }, { status: 500 });
  }

  return NextResponse.json({ received: batch.events.length, upserted: count ?? batch.events.length });
}

/**
 * "Started"/"Heartbeat" -> online, "Stopped" -> stopped. "offline" nunca é escrito pelo
 * agente — fica reservado para uma leitura futura que note um last_seen_at parado há
 * muito tempo (ver docs/supabase_schema.sql).
 */
function toDeviceStatus(state: DeviceStatusState): DeviceStatus {
  return state === "Stopped" ? "stopped" : "online";
}
