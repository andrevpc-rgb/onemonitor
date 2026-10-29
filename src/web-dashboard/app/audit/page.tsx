import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseAdminClient } from "@/lib/supabase/admin";
import type { AuditLogRow } from "@/lib/supabase/types";
import { formatDateTime } from "@/lib/dashboard/format";

const ACTION_LABELS: Record<string, string> = {
  "organization_settings.updated": "Configurações da organização alteradas",
  "device.revoked": "Acesso de dispositivo revogado",
  "device.restored": "Acesso de dispositivo restaurado",
};

export default function AuditPage() {
  return (
    <main className="mx-auto max-w-3xl space-y-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Auditoria</h1>
        <Link href="/" className="text-sm font-medium underline">
          Voltar ao dashboard
        </Link>
      </header>

      <Suspense fallback={<div className="h-64 animate-pulse rounded-lg bg-black/5" />}>
        <AuditData />
      </Suspense>
    </main>
  );
}

async function AuditData() {
  await connection();

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

  if (profile.role !== "owner" && profile.role !== "manager") {
    return (
      <p className="rounded-md p-3 text-sm" style={{ background: "#fdf3e1", color: "#6b4a06" }}>
        Só proprietários e gestores têm acesso à auditoria.
      </p>
    );
  }

  // RLS (audit_logs_select_own_organization) já restringe à própria organização e a
  // owner/manager — o filtro de role acima é só para dar uma mensagem clara em vez de uma
  // lista vazia sem explicação.
  const { data: logRows, error: logsError } = await supabase
    .from("audit_logs")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(100);

  if (logsError) {
    return <p className="text-sm text-red-600">Erro ao carregar o histórico: {logsError.message}</p>;
  }

  const logs = (logRows ?? []) as AuditLogRow[];
  const actorEmailById = await resolveActorEmails(logs);

  if (logs.length === 0) {
    return (
      <p className="text-sm" style={{ color: "#898781" }}>
        Nenhuma alteração registrada ainda.
      </p>
    );
  }

  return (
    <ul className="space-y-3">
      {logs.map((log) => (
        <li key={log.id} className="rounded-md border p-3 text-sm" style={{ borderColor: "#e1e0d9" }}>
          <div className="flex items-center justify-between gap-3">
            <span className="font-medium">{ACTION_LABELS[log.action] ?? log.action}</span>
            <span className="text-xs tabular-nums" style={{ color: "#898781" }}>
              {formatDateTime(log.created_at)}
            </span>
          </div>
          <p className="mt-1 text-xs" style={{ color: "#52514e" }}>
            {log.actor_id ? (actorEmailById.get(log.actor_id) ?? "Usuário removido") : "Sistema"}
            {log.target_id && ` · ${log.target_type}: ${log.target_id}`}
          </p>
        </li>
      ))}
    </ul>
  );
}

/** auth.users não é legível pelo client de sessão — resolve os e-mails só dos autores que
 * aparecem na página atual, com o client admin (server-only, página já restrita a owner/manager). */
async function resolveActorEmails(logs: AuditLogRow[]): Promise<Map<string, string>> {
  const distinctActorIds = [...new Set(logs.map((l) => l.actor_id).filter((id): id is string => !!id))];
  if (distinctActorIds.length === 0) {
    return new Map();
  }

  const admin = createSupabaseAdminClient();
  const entries = await Promise.all(
    distinctActorIds.map(async (id) => {
      const { data } = await admin.auth.admin.getUserById(id);
      return [id, data.user?.email ?? "Usuário removido"] as const;
    }),
  );

  return new Map(entries);
}
