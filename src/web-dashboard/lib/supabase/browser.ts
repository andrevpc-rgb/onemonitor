"use client";

import { createBrowserClient } from "@supabase/ssr";

/**
 * Client de navegador — só existe para app/auth/callback (ver comentário lá): o token do
 * link de convite/magic link do Supabase chega no fragmento da URL ("#access_token=..."),
 * que nunca é enviado ao servidor, então só dá pra ler e trocar pela sessão no browser.
 * createBrowserClient grava a sessão em cookies (document.cookie) na mesma convenção que
 * createSupabaseServerClient lê, então o resto do app (Server Components/Actions) continua
 * funcionando normalmente depois disso. Não use para mais nada — todo o resto do dashboard é
 * Server Components/Actions de propósito (ver lib/supabase/server.ts).
 */
export function createSupabaseBrowserClient() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const anonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

  if (!url || !anonKey) {
    throw new Error(
      "NEXT_PUBLIC_SUPABASE_URL e NEXT_PUBLIC_SUPABASE_ANON_KEY precisam estar definidas (ver .env.local.example).",
    );
  }

  return createBrowserClient(url, anonKey);
}
