"use server";

import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";

/**
 * Client de sessão (não admin): a policy de update de organization_settings (RLS, ver
 * docs/supabase_schema.sql) já exige owner/manager — isso reforça no banco a mesma regra
 * que a tela de /settings aplica na UI, em vez de confiar só no formulário escondido.
 */
export async function updateOrganizationSettings(formData: FormData) {
  const supabase = await createSupabaseServerClient();

  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { data: profile, error: profileError } = await supabase
    .from("profiles")
    .select("organization_id")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    redirect(`/settings?error=${encodeURIComponent("Perfil não encontrado.")}`);
    return;
  }

  const update = {
    work_start_time: String(formData.get("work_start_time") ?? ""),
    work_end_time: String(formData.get("work_end_time") ?? ""),
    mandatory_break_minutes: Number(formData.get("mandatory_break_minutes") ?? 0),
    variable_break_minutes: Number(formData.get("variable_break_minutes") ?? 0),
    enable_idle_popup: formData.get("enable_idle_popup") === "on",
    popup_idle_threshold_minutes: Number(formData.get("popup_idle_threshold_minutes") ?? 0),
    usage_events_retention_days: Number(formData.get("usage_events_retention_days") ?? 0),
    workload_alert_enabled: formData.get("workload_alert_enabled") === "on",
    workload_alert_daily_hours_threshold: Number(formData.get("workload_alert_daily_hours_threshold") ?? 0),
    workload_alert_consecutive_days_threshold: Number(formData.get("workload_alert_consecutive_days_threshold") ?? 0),
  };

  const { error } = await supabase
    .from("organization_settings")
    .update(update)
    .eq("organization_id", profile.organization_id);

  if (error) {
    redirect(`/settings?error=${encodeURIComponent(error.message)}`);
    return;
  }

  redirect("/settings?saved=1");
}

/**
 * Nome e logo vivem em `organizations` (não `organization_settings`): são identidade da
 * organização, não parâmetros operacionais de expediente/pausas. RLS (ver
 * organizations_update_own em docs/supabase_schema.sql) já exige owner/manager, igual a
 * updateOrganizationSettings acima.
 */
export async function updateOrganizationIdentity(formData: FormData) {
  const supabase = await createSupabaseServerClient();

  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { data: profile, error: profileError } = await supabase
    .from("profiles")
    .select("organization_id")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    redirect(`/settings?error=${encodeURIComponent("Perfil não encontrado.")}`);
    return;
  }

  const name = String(formData.get("organization_name") ?? "").trim();
  if (!name) {
    redirect(`/settings?error=${encodeURIComponent("O nome da organização não pode ficar vazio.")}`);
    return;
  }

  const update: { name: string; logo_url?: string } = { name };

  // O campo de logo é opcional em todo salvamento: um <input type="file"> vazio chega aqui
  // como um File de tamanho 0, nunca como null — por isso o filtro é por size, não por
  // existência, senão toda submissão do formulário (mesmo só mudando o nome) apagaria o
  // logo já salvo.
  const logoFile = formData.get("logo");
  if (logoFile instanceof File && logoFile.size > 0) {
    if (!logoFile.type.startsWith("image/")) {
      redirect(`/settings?error=${encodeURIComponent("O logo precisa ser um arquivo de imagem.")}`);
      return;
    }

    const extension = logoFile.name.includes(".") ? logoFile.name.split(".").pop() : "png";
    const path = `${profile.organization_id}/logo.${extension}`;

    const { error: uploadError } = await supabase.storage
      .from("organization-logos")
      .upload(path, logoFile, { upsert: true, contentType: logoFile.type });

    if (uploadError) {
      redirect(`/settings?error=${encodeURIComponent(`Erro ao enviar o logo: ${uploadError.message}`)}`);
      return;
    }

    const { data: publicUrlData } = supabase.storage.from("organization-logos").getPublicUrl(path);
    // Cache-busting: a URL pública é sempre a mesma para o mesmo path (upsert sobrescreve o
    // objeto), então sem isso o navegador/relatório continuaria mostrando o logo antigo em
    // cache depois de uma troca.
    update.logo_url = `${publicUrlData.publicUrl}?v=${Date.now()}`;
  }

  const { error } = await supabase.from("organizations").update(update).eq("id", profile.organization_id);

  if (error) {
    redirect(`/settings?error=${encodeURIComponent(error.message)}`);
    return;
  }

  redirect("/settings?saved=1");
}
