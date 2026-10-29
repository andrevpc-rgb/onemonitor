import { NextResponse, type NextRequest } from "next/server";
import { createServerClient } from "@supabase/ssr";

// Páginas de autenticação: sem sessão, ficam acessíveis; com sessão, redirecionam pra "/"
// (não faz sentido logar de novo ou recriar organização já tendo uma sessão ativa).
const AUTH_PATHS = ["/login", "/signup"];

// Sempre acessíveis, independente de sessão — nunca redirecionadas em nenhum sentido.
// /auth/callback troca o "code" do link de convite pela sessão (precisa rodar sem sessão
// prévia: é ela quem cria a sessão). /privacy e /terms são públicas por natureza (link no
// rodapé de login/cadastro, mas também podem ser abertas por quem já está logado).
const ALWAYS_PUBLIC_PATHS = ["/auth/callback", "/privacy", "/terms"];

/**
 * Renova a sessão do Supabase Auth a cada requisição (os tokens expiram e precisam ser
 * trocados pelo refresh token guardado no cookie) e protege o dashboard: sem sessão, manda
 * para /login; com sessão, tira de /login e /signup. A ingestão de sync (/api/sync) usa sua
 * própria autenticação por api_key, por isso fica de fora do matcher abaixo.
 */
export async function proxy(request: NextRequest) {
  let response = NextResponse.next({ request });

  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const anonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if (!url || !anonKey) {
    return response;
  }

  const supabase = createServerClient(url, anonKey, {
    cookies: {
      getAll() {
        return request.cookies.getAll();
      },
      setAll(cookiesToSet) {
        for (const { name, value } of cookiesToSet) {
          request.cookies.set(name, value);
        }
        response = NextResponse.next({ request });
        for (const { name, value, options } of cookiesToSet) {
          response.cookies.set(name, value, options);
        }
      },
    },
  });

  const { data: { user } } = await supabase.auth.getUser();
  const pathname = request.nextUrl.pathname;
  const isAlwaysPublic = ALWAYS_PUBLIC_PATHS.some((path) => pathname.startsWith(path));
  const isAuthPath = AUTH_PATHS.some((path) => pathname.startsWith(path));

  if (isAlwaysPublic) {
    return response;
  }

  if (!user && !isAuthPath) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  if (user && isAuthPath) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return response;
}

export const config = {
  matcher: ["/((?!api/sync|_next/static|_next/image|favicon.ico).*)"],
};
