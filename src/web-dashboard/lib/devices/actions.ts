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
