"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";

/**
 * Client de sessão (não admin): a policy de update de devices (RLS, ver
 * docs/supabase_schema.sql) já exige owner/manager da mesma organização — reforça no banco
 * a mesma regra que a tela /devices aplica na UI.
 */
async function setRevokedAt(deviceRowId: string, revokedAt: string | null) {
  const supabase = await createSupabaseServerClient();

  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { error } = await supabase.from("devices").update({ revoked_at: revokedAt }).eq("id", deviceRowId);

  if (error) {
    redirect(`/devices?error=${encodeURIComponent(error.message)}`);
    return;
  }

  revalidatePath("/devices");
  redirect("/devices");
}

export async function revokeDevice(formData: FormData) {
  const deviceRowId = String(formData.get("deviceRowId") ?? "");
  await setRevokedAt(deviceRowId, new Date().toISOString());
}

export async function restoreDevice(formData: FormData) {
  const deviceRowId = String(formData.get("deviceRowId") ?? "");
  await setRevokedAt(deviceRowId, null);
}

/**
 * Renomeia o rótulo exibido do dispositivo (device_name) — depois disso o agente nunca
 * mais sobrescreve esse campo automaticamente (ver app/api/sync/route.ts), só volta a
 * mudar se alguém renomear de novo por aqui.
 */
export async function renameDevice(formData: FormData) {
  const deviceRowId = String(formData.get("deviceRowId") ?? "");
  const name = String(formData.get("deviceName") ?? "").trim();

  if (!name) {
    redirect(`/devices?error=${encodeURIComponent("O nome não pode ficar vazio.")}`);
    return;
  }

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { error } = await supabase.from("devices").update({ device_name: name }).eq("id", deviceRowId);

  if (error) {
    redirect(`/devices?error=${encodeURIComponent(error.message)}`);
    return;
  }

  revalidatePath("/devices");
  redirect("/devices");
}

/**
 * Define o setor/equipe do dispositivo (texto livre, usado pelo grid de produtividade do
 * dashboard para agrupar/filtrar) — campo em branco limpa o setor (volta a "sem setor").
 */
export async function updateDeviceSector(formData: FormData) {
  const deviceRowId = String(formData.get("deviceRowId") ?? "");
  const sector = String(formData.get("sector") ?? "").trim();

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { error } = await supabase.from("devices").update({ sector: sector || null }).eq("id", deviceRowId);

  if (error) {
    redirect(`/devices?error=${encodeURIComponent(error.message)}`);
    return;
  }

  revalidatePath("/devices");
  revalidatePath("/");
  redirect("/devices");
}

/**
 * Exclusão definitiva — só permitida para um dispositivo já revogado (reforçado aqui, não só
 * escondendo o botão na UI): revogar bloqueia sync futuro, mas não apaga nada; isto é
 * irreversível, então exige o passo de revogação antes como confirmação implícita. Apaga
 * primeiro os usage_events do dispositivo (FK em devices.device_id, ver
 * docs/supabase_schema.sql) e só então o próprio registro.
 */
export async function deleteDevice(formData: FormData) {
  const deviceRowId = String(formData.get("deviceRowId") ?? "");

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { data: device, error: deviceError } = await supabase
    .from("devices")
    .select("device_id, revoked_at")
    .eq("id", deviceRowId)
    .single();

  if (deviceError || !device) {
    redirect(`/devices?error=${encodeURIComponent(deviceError?.message ?? "Dispositivo não encontrado.")}`);
    return;
  }

  if (!device.revoked_at) {
    redirect(`/devices?error=${encodeURIComponent("Revogue o acesso do dispositivo antes de excluí-lo.")}`);
    return;
  }

  const { error: eventsError } = await supabase.from("usage_events").delete().eq("device_id", device.device_id);
  if (eventsError) {
    redirect(`/devices?error=${encodeURIComponent(eventsError.message)}`);
    return;
  }

  const { error: deleteError } = await supabase.from("devices").delete().eq("id", deviceRowId);
  if (deleteError) {
    redirect(`/devices?error=${encodeURIComponent(deleteError.message)}`);
    return;
  }

  revalidatePath("/devices");
  revalidatePath("/");
  redirect("/devices");
}
