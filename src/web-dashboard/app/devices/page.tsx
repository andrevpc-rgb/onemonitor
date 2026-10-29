import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { revokeDevice, restoreDevice } from "@/lib/devices/actions";
import type { DeviceRow } from "@/lib/supabase/types";
import { formatDate, formatDateTime } from "@/lib/dashboard/format";

type SearchParams = Promise<{ error?: string }>;

export default function DevicesPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto max-w-3xl space-y-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Dispositivos</h1>
        <Link href="/" className="text-sm font-medium underline">
          Voltar ao dashboard
        </Link>
      </header>

      <Suspense fallback={<div className="h-64 animate-pulse rounded-lg bg-black/5" />}>
        <DevicesData searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function DevicesData({ searchParams }: { searchParams: SearchParams }) {
  await connection();

  const { error } = await searchParams;

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

  const { data: deviceRows, error: devicesError } = await supabase
    .from("devices")
    .select("*")
    .order("hostname", { ascending: true });

  if (devicesError) {
    return <p className="text-sm text-red-600">Erro ao carregar dispositivos: {devicesError.message}</p>;
  }

  const devices = (deviceRows ?? []) as DeviceRow[];
  const canManage = profile.role === "owner" || profile.role === "manager";

  return (
    <div className="space-y-4">
      {error && <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      {!canManage && (
        <p className="rounded-md p-3 text-sm" style={{ background: "#fdf3e1", color: "#6b4a06" }}>
          Só proprietários e gestores podem revogar o acesso de um dispositivo — você está
          vendo a lista como leitura.
        </p>
      )}

      {devices.length === 0 ? (
        <p className="text-sm" style={{ color: "#898781" }}>
          Nenhum dispositivo sincronizou dados ainda.
        </p>
      ) : (
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b text-xs" style={{ color: "#898781" }}>
              <th className="py-2 pr-3 font-normal">Dispositivo</th>
              <th className="py-2 pr-3 font-normal">Visto pela última vez</th>
              <th className="py-2 pr-3 font-normal">Monitorando desde</th>
              <th className="py-2 pr-3 font-normal">Status</th>
              {canManage && <th className="py-2 text-right font-normal">Ação</th>}
            </tr>
          </thead>
          <tbody>
            {devices.map((device) => {
              const revoked = !!device.revoked_at;
              return (
                <tr key={device.id} className="border-b">
                  <td className="py-2 pr-3 font-medium">{device.hostname || device.device_id}</td>
                  <td className="py-2 pr-3 tabular-nums" style={{ color: "#52514e" }}>
                    {formatDateTime(device.last_seen_at)}
                  </td>
                  <td className="py-2 pr-3 tabular-nums" style={{ color: "#52514e" }}>
                    {formatDate(device.first_seen_at)}
                  </td>
                  <td className="py-2 pr-3">
                    {revoked ? (
                      <span className="font-medium text-red-700">
                        Revogado em {formatDateTime(device.revoked_at!)}
                      </span>
                    ) : (
                      <span style={{ color: "#006300" }}>Ativo</span>
                    )}
                  </td>
                  {canManage && (
                    <td className="py-2 text-right">
                      <form action={revoked ? restoreDevice : revokeDevice}>
                        <input type="hidden" name="deviceRowId" value={device.id} />
                        <button
                          type="submit"
                          className="text-xs font-medium underline"
                          style={{ color: revoked ? "#006300" : "#d03b3b" }}
                        >
                          {revoked ? "Restaurar acesso" : "Revogar acesso"}
                        </button>
                      </form>
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}
