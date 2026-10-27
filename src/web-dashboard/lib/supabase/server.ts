import { cookies } from "next/headers";
import { createServerClient } from "@supabase/ssr";

/**
 * Client vinculado à sessão do usuário autenticado (cookies via @supabase/ssr) — respeita
 * RLS normalmente. Use em Server Components e Server Actions que leem/escrevem dados em
 * nome do usuário logado (o dashboard, por exemplo). Nunca use este client para a ingestão
 * de sync nem para o bootstrap de cadastro — esses dois precisam ignorar RLS de propósito
 * e devem usar ./admin.
 */
export async function createSupabaseServerClient() {
  const cookieStore = await cookies();
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const anonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

  if (!url || !anonKey) {
    throw new Error(
      "NEXT_PUBLIC_SUPABASE_URL e NEXT_PUBLIC_SUPABASE_ANON_KEY precisam estar definidas (ver .env.local.example).",
    );
  }

  return createServerClient(url, anonKey, {
    cookies: {
      getAll() {
        return cookieStore.getAll();
      },
      setAll(cookiesToSet) {
        try {
          for (const { name, value, options } of cookiesToSet) {
            cookieStore.set(name, value, options);
          }
        } catch {
          // Chamado de dentro de um Server Component (sem permissão de escrever cookies) —
          // inofensivo aqui porque o middleware já renova a sessão a cada requisição.
        }
      },
    },
  });
}
