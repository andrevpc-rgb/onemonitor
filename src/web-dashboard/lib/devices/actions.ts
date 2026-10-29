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
