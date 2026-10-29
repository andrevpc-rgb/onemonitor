import { Suspense } from "react";
import { completeInvite } from "@/lib/auth/actions";

type SearchParams = Promise<{ error?: string }>;

export default function SetPasswordPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto flex min-h-full max-w-sm flex-col justify-center gap-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">Bem-vindo(a) ao OneMonitor</h1>
        <p className="text-sm" style={{ color: "#898781" }}>
          Defina uma senha para acessar sua conta.
        </p>
      </header>

      <Suspense fallback={null}>
        <SetPasswordError searchParams={searchParams} />
      </Suspense>

      <form action={completeInvite} className="space-y-4">
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

        <div className="space-y-1">
          <label htmlFor="confirmPassword" className="text-sm font-medium">
            Confirmar senha
          </label>
          <input
            id="confirmPassword"
            name="confirmPassword"
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
          Continuar
        </button>
      </form>
    </main>
  );
}

async function SetPasswordError({ searchParams }: { searchParams: SearchParams }) {
  const { error } = await searchParams;

  if (!error) {
    return null;
  }

  return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>;
}
