import { NextResponse } from "next/server";
import { createSupabaseServerClient } from "@/lib/supabase/server";

/**
 * Destino do link de convite (auth.admin.inviteUserByEmail, ver lib/team/actions.ts) e de
 * qualquer outro fluxo de auth do Supabase baseado em "code" (PKCE) — troca o code pela
 * sessão (grava os cookies via o client de sessão) e manda para completar o cadastro.
 */
export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get("code");

  if (!code) {
    return NextResponse.redirect(`${origin}/login?error=${encodeURIComponent("Link de convite inválido ou expirado.")}`);
  }

  const supabase = await createSupabaseServerClient();
  const { error } = await supabase.auth.exchangeCodeForSession(code);

  if (error) {
    return NextResponse.redirect(`${origin}/login?error=${encodeURIComponent(error.message)}`);
  }

  return NextResponse.redirect(`${origin}/invite/set-password`);
}
