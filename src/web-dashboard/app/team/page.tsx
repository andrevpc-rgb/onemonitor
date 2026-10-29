import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseAdminClient } from "@/lib/supabase/admin";
import { inviteMember } from "@/lib/team/actions";
import type { ProfileRow } from "@/lib/supabase/types";

type SearchParams = Promise<{ error?: string; invited?: string }>;

const ROLE_LABELS: Record<string, string> = { owner: "Proprietário", manager: "Gestor", employee: "Colaborador" };

export default function TeamPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto max-w-2xl space-y-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Equipe</h1>
        <Link href="/" className="text-sm font-medium underline">
          Voltar ao dashboard
        </Link>
      </header>

      <Suspense fallback={<div className="h-64 animate-pulse rounded-lg bg-black/5" />}>
        <TeamData searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function TeamData({ searchParams }: { searchParams: SearchParams }) {
  await connection();

  const { error, invited } = await searchParams;

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    return <p className="text-sm text-red-600">Sessão inválida.</p>;
  }

  const { data: profile, error: profileError } = await supabase
    .from("profiles")
    .select("role")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    return <p className="text-sm text-red-600">Erro ao carregar seu perfil: {profileError?.message}</p>;
  }

  const { data: memberRows, error: membersError } = await supabase
    .from("profiles")
    .select("*")
    .order("created_at", { ascending: true });

  if (membersError) {
    return <p className="text-sm text-red-600">Erro ao carregar a equipe: {membersError.message}</p>;
  }

  const members = (memberRows ?? []) as ProfileRow[];
  const canInvite = profile.role === "owner" || profile.role === "manager";
  const memberDetails = await resolveMemberDetails(members);

  return (
    <div className="space-y-6">
      {invited && (
        <p className="rounded-md p-3 text-sm" style={{ background: "#e9f7ef", color: "#006300" }}>
          Convite enviado. A pessoa recebe um e-mail para definir a senha e entrar.
        </p>
      )}
      {error && <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}

      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b text-xs" style={{ color: "#898781" }}>
            <th className="py-2 pr-3 font-normal">Membro</th>
            <th className="py-2 pr-3 font-normal">Papel</th>
            <th className="py-2 font-normal">Status</th>
          </tr>
        </thead>
        <tbody>
          {members.map((member) => {
            const details = memberDetails.get(member.id);
            return (
              <tr key={member.id} className="border-b">
                <td className="py-2 pr-3">{details?.email ?? "—"}</td>
                <td className="py-2 pr-3">{ROLE_LABELS[member.role] ?? member.role}</td>
                <td className="py-2">
                  {details?.confirmed ? (
                    <span style={{ color: "#006300" }}>Ativo</span>
                  ) : (
                    <span style={{ color: "#6b4a06" }}>Convite pendente</span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {canInvite && (
        <form action={inviteMember} className="space-y-3 rounded-lg border p-4" style={{ borderColor: "#e1e0d9" }}>
          <h2 className="text-sm font-semibold">Convidar membro</h2>
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 space-y-1" style={{ minWidth: 200 }}>
              <label htmlFor="email" className="text-sm font-medium">
                E-mail
              </label>
              <input
                id="email"
                name="email"
                type="email"
                required
                className="w-full rounded-md border px-3 py-2 text-sm"
                style={{ borderColor: "#e1e0d9" }}
              />
            </div>
            <div className="space-y-1">
              <label htmlFor="role" className="text-sm font-medium">
                Papel
              </label>
              <select
                id="role"
                name="role"
                defaultValue="employee"
                className="rounded-md border px-2 py-2 text-sm"
                style={{ borderColor: "#e1e0d9" }}
              >
                <option value="employee">Colaborador</option>
                <option value="manager">Gestor</option>
              </select>
            </div>
            <button
              type="submit"
              className="rounded-md px-3 py-2 text-sm font-medium text-white"
              style={{ background: "#0b0b0b" }}
            >
              Convidar
            </button>
          </div>
        </form>
      )}
    </div>
  );
}

/** auth.users não é legível pelo client de sessão — resolve e-mail/confirmação só dos
 * membros da página atual, com o client admin (server-only). */
async function resolveMemberDetails(members: ProfileRow[]): Promise<Map<string, { email: string; confirmed: boolean }>> {
  if (members.length === 0) {
    return new Map();
  }

  const admin = createSupabaseAdminClient();
  const entries = await Promise.all(
    members.map(async (member) => {
      const { data } = await admin.auth.admin.getUserById(member.id);
      return [
        member.id,
        { email: data.user?.email ?? "Usuário removido", confirmed: !!data.user?.email_confirmed_at },
      ] as const;
    }),
  );

  return new Map(entries);
}
