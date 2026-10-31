"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { createSupabaseBrowserClient } from "@/lib/supabase/browser";

/**
 * Destino do link de convite (auth.admin.inviteUserByEmail, ver lib/team/actions.ts) e de
 * qualquer outro link de ação do Supabase Auth gerado pela Admin API (magic link, recovery).
 *
 * Esses links SEMPRE entregam o token no fragmento da URL ("#access_token=...&refresh_token=
 * ...&type=invite"), nunca como "?code=" — a API Admin gera o link sozinha, sem um client no
 * meio para negociar o code_challenge que o fluxo PKCE exige, então não tem como ser PKCE.
 * Fragmento nunca é enviado ao servidor (é só do browser), então isso só pode ser lido e
 * trocado pela sessão aqui, client-side — daí "use client" em vez de um Route Handler.
 */
export default function AuthCallbackPage() {
  const router = useRouter();

  useEffect(() => {
    const hash = window.location.hash.startsWith("#") ? window.location.hash.slice(1) : window.location.hash;
    const params = new URLSearchParams(hash);

    const errorDescription = params.get("error_description");
    if (errorDescription) {
      router.replace(`/login?error=${encodeURIComponent(errorDescription)}`);
      return;
    }

    const accessToken = params.get("access_token");
    const refreshToken = params.get("refresh_token");
    if (!accessToken || !refreshToken) {
      router.replace(`/login?error=${encodeURIComponent("Link de convite inválido ou expirado.")}`);
      return;
    }

    const supabase = createSupabaseBrowserClient();
    supabase.auth.setSession({ access_token: accessToken, refresh_token: refreshToken }).then(({ error }) => {
      if (error) {
        router.replace(`/login?error=${encodeURIComponent(error.message)}`);
        return;
      }
      router.replace("/invite/set-password");
    });
  }, [router]);

  return (
    <main className="flex min-h-full items-center justify-center p-6">
      <p className="text-sm" style={{ color: "#898781" }}>
        Entrando...
      </p>
    </main>
  );
}
