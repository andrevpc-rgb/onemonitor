import { createClient } from "@supabase/supabase-js";

/**
 * Client com a service-role key — ignora RLS por definição no Supabase/Postgres.
 *
 * NUNCA importe este arquivo de um Client Component ("use client") nem exponha
 * SUPABASE_SERVICE_ROLE_KEY com o prefixo NEXT_PUBLIC_. Use só em Server Components e
 * Route Handlers (app/api/**), onde o código roda exclusivamente no servidor.
 *
 * Sem generic de tipos do schema aqui de propósito — o shape exato que o supabase-js
 * espera para tipar query builders é mais rígido do que vale a pena perseguir num MVP;
 * os tipos de linha (DeviceRow/UsageEventRow, em ./types) são aplicados manualmente nos
 * pontos de consumo (app/api/sync/route.ts, app/page.tsx).
 */
export function createSupabaseServerClient() {
  const url = process.env.SUPABASE_URL;
  const serviceRoleKey = process.env.SUPABASE_SERVICE_ROLE_KEY;

  if (!url || !serviceRoleKey) {
    throw new Error(
      "SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY precisam estar definidas (ver .env.local.example).",
    );
  }

  return createClient(url, serviceRoleKey, {
    auth: { persistSession: false },
  });
}
