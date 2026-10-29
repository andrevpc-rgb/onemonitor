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
