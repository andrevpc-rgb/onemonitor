import { NextResponse } from "next/server";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { isValidSyncBatch, type SyncBatch } from "@/lib/sync/contracts";
import type { UsageEventInsert } from "@/lib/supabase/types";

/**
 * Ingestão de lotes enviados pelo agente desktop C# (SyncService -> HttpSyncApiClient).
 *
 * Os códigos de status aqui precisam continuar alinhados com a classificação que
 * HttpSyncApiClient já faz do lado do agente: 2xx = sucesso; 4xx = falha permanente (não
 * adianta reenviar o mesmo lote); 5xx = falha transitória (o agente retenta com backoff).
 */
export async function POST(request: Request) {
  const apiKey = request.headers.get("x-api-key");
  const expectedApiKey = process.env.SYNC_API_KEY;

  if (!expectedApiKey) {
    // Backend mal configurado — não é culpa do agente, mas também não é algo que reenviar
    // o lote resolve sozinho; ainda assim, como 5xx, o agente vai retentar (razoável: um
    // operador pode corrigir a env var a qualquer momento).
    return NextResponse.json({ error: "SYNC_API_KEY não configurada no servidor." }, { status: 500 });
  }

  if (apiKey !== expectedApiKey) {
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
  const supabase = createSupabaseServerClient();

  const { error: deviceError } = await supabase
    .from("devices")
    .upsert(
      { device_id: batch.deviceId, last_seen_at: new Date().toISOString() },
      { onConflict: "device_id" },
    );

  if (deviceError) {
    return NextResponse.json({ error: deviceError.message }, { status: 500 });
  }

  if (batch.events.length === 0) {
    return NextResponse.json({ received: 0, upserted: 0 });
  }

  const rows: UsageEventInsert[] = batch.events.map((event) => ({
    event_id: event.eventId,
    device_id: batch.deviceId,
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
