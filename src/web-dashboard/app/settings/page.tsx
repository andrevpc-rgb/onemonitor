import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { updateOrganizationSettings, updateOrganizationIdentity } from "@/lib/settings/actions";
import {
  DEFAULT_ORGANIZATION_SETTINGS,
  calculateNetTargetMinutes,
  formatMinutesAsHours,
} from "@/lib/dashboard/workPolicy";

type SearchParams = Promise<{ saved?: string; error?: string }>;

const inputClass = "w-full rounded-md border px-3 py-2 text-sm disabled:opacity-60";

export default function SettingsPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto max-w-2xl space-y-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Configurações da organização</h1>
        <Link href="/" className="text-sm font-medium underline">
          Voltar ao dashboard
        </Link>
      </header>

      <Suspense fallback={<div className="h-64 animate-pulse rounded-lg bg-black/5" />}>
        <SettingsData searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function SettingsData({ searchParams }: { searchParams: SearchParams }) {
  // Ver comentário equivalente em app/page.tsx — busca de dados isolada num Suspense
  // dinâmico (Next.js 16 Cache Components).
  await connection();

  const { saved, error } = await searchParams;

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    return <p className="text-sm text-red-600">Sessão inválida.</p>;
  }

  const { data: profile, error: profileError } = await supabase
    .from("profiles")
    .select("organization_id, role")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    return <p className="text-sm text-red-600">Erro ao carregar seu perfil: {profileError?.message}</p>;
  }

  const { data: organization, error: organizationError } = await supabase
    .from("organizations")
    .select("name, logo_url, api_key")
    .eq("id", profile.organization_id)
    .single();

  if (organizationError || !organization) {
    return <p className="text-sm text-red-600">Erro ao carregar a organização: {organizationError?.message}</p>;
  }

  const { data: settingsRow, error: settingsError } = await supabase
    .from("organization_settings")
    .select("*")
    .eq("organization_id", profile.organization_id)
    .maybeSingle();

  if (settingsError) {
    return <p className="text-sm text-red-600">Erro ao carregar configurações: {settingsError.message}</p>;
  }

  const settings = settingsRow ?? DEFAULT_ORGANIZATION_SETTINGS;
  const canEdit = profile.role === "owner" || profile.role === "manager";
  const netMinutes = calculateNetTargetMinutes(settings);

  return (
    <div className="space-y-4">
      {saved && (
        <p className="rounded-md p-3 text-sm" style={{ background: "#e9f7ef", color: "#006300" }}>
          Configurações salvas.
        </p>
      )}
      {error && (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>
      )}
      {!canEdit && (
        <p className="rounded-md p-3 text-sm" style={{ background: "#fdf3e1", color: "#6b4a06" }}>
          Só proprietários e gestores podem alterar estas configurações — você está vendo os
          valores atuais.
        </p>
      )}

      <section className="space-y-4 rounded-lg border p-4">
        <h2 className="text-sm font-semibold">Identidade da organização</h2>
        <p className="text-xs" style={{ color: "#898781" }}>
          Nome e logo exibidos no cabeçalho do dashboard e nos relatórios gerados pelo
          agente desktop.
        </p>

        <div className="space-y-1">
          <span className="text-sm font-medium">Chave de API do agente (Sync.ApiKey)</span>
          <input
            type="text"
            readOnly
            value={organization.api_key}
            onFocus={(e) => e.currentTarget.select()}
            className={`${inputClass} font-mono`}
          />
          <p className="text-xs" style={{ color: "#898781" }}>
            Cole este valor em <code>Sync.ApiKey</code> no <code>appsettings.Local.json</code>
            {" "}de cada computador que for monitorado por esta organização.
          </p>
        </div>

        <form action={updateOrganizationIdentity} className="space-y-4" encType="multipart/form-data">
          <div className="space-y-1">
            <label htmlFor="organization_name" className="text-sm font-medium">
              Nome da organização
            </label>
            <input
              id="organization_name"
              name="organization_name"
              type="text"
              required
              maxLength={120}
              disabled={!canEdit}
              defaultValue={organization.name}
              className={inputClass}
            />
          </div>

          <div className="space-y-1">
            <label htmlFor="logo" className="text-sm font-medium">
              Logo
            </label>
            <div className="flex items-center gap-3">
              {organization.logo_url && (
                // eslint-disable-next-line @next/next/no-img-element -- URL externa (Supabase Storage), sem benefício de next/image aqui
                <img
                  src={organization.logo_url}
                  alt={`Logo de ${organization.name}`}
                  className="h-12 w-12 rounded border object-contain"
                />
              )}
              <input
                id="logo"
                name="logo"
                type="file"
                accept="image/*"
                disabled={!canEdit}
                className="text-sm"
              />
            </div>
            <p className="text-xs" style={{ color: "#898781" }}>
              Imagem (PNG, JPG ou SVG). Deixe em branco para manter o logo atual.
            </p>
          </div>

          {canEdit && (
            <button
              type="submit"
              className="rounded-md px-3 py-2 text-sm font-medium text-white"
              style={{ background: "#0b0b0b" }}
            >
              Salvar identidade
            </button>
          )}
        </form>
      </section>

      <p className="text-sm" style={{ color: "#52514e" }}>
        Jornada líquida alvo: <strong>{formatMinutesAsHours(netMinutes)}</strong> (expediente
        menos as pausas obrigatória e variável).
      </p>

      <form action={updateOrganizationSettings} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1">
            <label htmlFor="work_start_time" className="text-sm font-medium">
              Início do expediente
            </label>
            <input
              id="work_start_time"
              name="work_start_time"
              type="time"
              required
              disabled={!canEdit}
              defaultValue={settings.work_start_time.slice(0, 5)}
              className={inputClass}
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="work_end_time" className="text-sm font-medium">
              Fim do expediente
            </label>
            <input
              id="work_end_time"
              name="work_end_time"
              type="time"
              required
              disabled={!canEdit}
              defaultValue={settings.work_end_time.slice(0, 5)}
              className={inputClass}
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1">
            <label htmlFor="mandatory_break_minutes" className="text-sm font-medium">
              Pausa obrigatória (min)
            </label>
            <input
              id="mandatory_break_minutes"
              name="mandatory_break_minutes"
              type="number"
              min={0}
              required
              disabled={!canEdit}
              defaultValue={settings.mandatory_break_minutes}
              className={inputClass}
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="variable_break_minutes" className="text-sm font-medium">
              Pausas variáveis (min)
            </label>
            <input
              id="variable_break_minutes"
              name="variable_break_minutes"
              type="number"
              min={0}
              required
              disabled={!canEdit}
              defaultValue={settings.variable_break_minutes}
              className={inputClass}
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <input
            id="enable_idle_popup"
            name="enable_idle_popup"
            type="checkbox"
            disabled={!canEdit}
            defaultChecked={settings.enable_idle_popup}
            className="h-4 w-4"
          />
          <label htmlFor="enable_idle_popup" className="text-sm font-medium">
            Exibir pop-up de justificativa ao retornar de uma ausência
          </label>
        </div>

        <div className="space-y-1">
          <label htmlFor="popup_idle_threshold_minutes" className="text-sm font-medium">
            Disparar o pop-up a partir de (min de inatividade)
          </label>
          <input
            id="popup_idle_threshold_minutes"
            name="popup_idle_threshold_minutes"
            type="number"
            min={0}
            required
            disabled={!canEdit}
            defaultValue={settings.popup_idle_threshold_minutes}
            className={inputClass}
          />
          <p className="text-xs" style={{ color: "#898781" }}>
            O pop-up nunca aparece fora do horário de expediente definido acima, nem nos
            fins de semana.
          </p>
        </div>

        <div className="space-y-1">
          <label htmlFor="usage_events_retention_days" className="text-sm font-medium">
            Reter dados de atividade por (dias)
          </label>
          <input
            id="usage_events_retention_days"
            name="usage_events_retention_days"
            type="number"
            min={1}
            required
            disabled={!canEdit}
            defaultValue={settings.usage_events_retention_days}
            className={inputClass}
          />
          <p className="text-xs" style={{ color: "#898781" }}>
            Eventos de atividade mais antigos que isso são apagados automaticamente, todo
            dia, por uma rotina agendada no banco.
          </p>
        </div>

        {canEdit && (
          <button
            type="submit"
            className="rounded-md px-3 py-2 text-sm font-medium text-white"
            style={{ background: "#0b0b0b" }}
          >
            Salvar
          </button>
        )}
      </form>
    </div>
  );
}
