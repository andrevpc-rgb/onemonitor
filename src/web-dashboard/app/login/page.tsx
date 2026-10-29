import { Suspense } from "react";
import Link from "next/link";
import { login } from "@/lib/auth/actions";
import { LegalFooter } from "@/components/LegalFooter";

type SearchParams = Promise<{ error?: string }>;

export default function LoginPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto flex min-h-full max-w-sm flex-col justify-center gap-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">OneMonitor — Entrar</h1>
      </header>

      <Suspense fallback={null}>
        <LoginError searchParams={searchParams} />
      </Suspense>

      <form action={login} className="space-y-4">
        <div className="space-y-1">
          <label htmlFor="email" className="text-sm font-medium">
            E-mail
          </label>
          <input
            id="email"
            name="email"
            type="email"
            required
            autoComplete="email"
            className="w-full rounded-md border px-3 py-2 text-sm"
          />
        </div>

        <div className="space-y-1">
          <label htmlFor="password" className="text-sm font-medium">
            Senha
          </label>
          <input
            id="password"
            name="password"
            type="password"
            required
            autoComplete="current-password"
            className="w-full rounded-md border px-3 py-2 text-sm"
          />
        </div>

        <button
          type="submit"
          className="w-full rounded-md bg-black px-3 py-2 text-sm font-medium text-white"
        >
          Entrar
        </button>
      </form>

      <p className="text-sm" style={{ color: "#898781" }}>
        Ainda não tem conta?{" "}
        <Link href="/signup" className="font-medium underline">
          Cadastre sua organização
        </Link>
      </p>

      <LegalFooter />
    </main>
  );
}

// Ler searchParams é um acesso dinâmico (Next.js 16 Cache Components): precisa ficar
// isolado num componente à parte, dentro de Suspense, para não bloquear o prerender do
// resto da página estática (o formulário em si não depende de nada dinâmico).
async function LoginError({ searchParams }: { searchParams: SearchParams }) {
  const { error } = await searchParams;

  if (!error) {
    return null;
  }

  return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>;
}
