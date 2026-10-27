import { Suspense } from "react";
import Link from "next/link";
import { signup } from "@/lib/auth/actions";

type SearchParams = Promise<{ error?: string }>;

export default function SignupPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto flex min-h-full max-w-sm flex-col justify-center gap-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">OneMonitor — Cadastrar organização</h1>
        <p className="text-sm" style={{ color: "#898781" }}>
          Cria sua organização e o primeiro usuário, com papel de proprietário.
        </p>
      </header>

      <Suspense fallback={null}>
        <SignupError searchParams={searchParams} />
      </Suspense>

      <form action={signup} className="space-y-4">
        <div className="space-y-1">
          <label htmlFor="organizationName" className="text-sm font-medium">
            Nome da organização
          </label>
          <input
            id="organizationName"
            name="organizationName"
            type="text"
            required
            className="w-full rounded-md border px-3 py-2 text-sm"
          />
        </div>

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
            minLength={6}
            autoComplete="new-password"
            className="w-full rounded-md border px-3 py-2 text-sm"
          />
        </div>

        <button
          type="submit"
          className="w-full rounded-md bg-black px-3 py-2 text-sm font-medium text-white"
        >
          Criar organização
        </button>
      </form>

      <p className="text-sm" style={{ color: "#898781" }}>
        Já tem conta?{" "}
        <Link href="/login" className="font-medium underline">
          Entrar
        </Link>
      </p>
    </main>
  );
}

// Ver comentário equivalente em app/login/page.tsx: searchParams é um acesso dinâmico e
// precisa ficar isolado num componente à parte, dentro de Suspense.
async function SignupError({ searchParams }: { searchParams: SearchParams }) {
  const { error } = await searchParams;

  if (!error) {
    return null;
  }

  return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>;
}
