"use server";

import { headers } from "next/headers";
import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseAdminClient } from "@/lib/supabase/admin";
import type { OrganizationRole } from "@/lib/supabase/types";

const INVITABLE_ROLES: OrganizationRole[] = ["manager", "employee"];

/**
 * Convite de membro: client de sessão só para checar quem está pedindo (precisa ser
 * owner/manager da própria organização — RLS não ajuda aqui porque o resto do fluxo usa o
 * client admin de propósito, então o controle de acesso é feito em código, não no banco).
 *
 * auth.admin.inviteUserByEmail é a forma de enviar e-mail já disponível neste projeto (sem
 * integração própria de e-mail) — cria o usuário (ainda sem senha) e manda o link que volta
 * para app/auth/callback. O profile é criado na hora, já vinculado a esse usuário; se o
 * convite nunca for aceito, fica "pendente" (sem e-mail confirmado) para sempre — não há
 * problema em deixar assim, mas dá pra limpar manualmente se precisar.
 */
export async function inviteMember(formData: FormData) {
  const email = String(formData.get("email") ?? "").trim();
  const roleInput = String(formData.get("role") ?? "");

  if (!email || !INVITABLE_ROLES.includes(roleInput as OrganizationRole)) {
    redirect(`/team?error=${encodeURIComponent("Preencha um e-mail e um papel válidos.")}`);
    return;
  }
  const role = roleInput as OrganizationRole;

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { data: profile, error: profileError } = await supabase
    .from("profiles")
    .select("organization_id, role")
    .eq("id", userData.user.id)
    .single();

  if (profileError || !profile) {
    redirect(`/team?error=${encodeURIComponent("Perfil não encontrado.")}`);
    return;
  }

  if (profile.role !== "owner" && profile.role !== "manager") {
    redirect(`/team?error=${encodeURIComponent("Só proprietários e gestores podem convidar membros.")}`);
    return;
  }

  const requestHeaders = await headers();
  const host = requestHeaders.get("host");
  const protocol = requestHeaders.get("x-forwarded-proto") ?? "http";
  const redirectTo = `${protocol}://${host}/auth/callback`;

  const admin = createSupabaseAdminClient();
  const { data: invited, error: inviteError } = await admin.auth.admin.inviteUserByEmail(email, { redirectTo });

  if (inviteError || !invited.user) {
    redirect(`/team?error=${encodeURIComponent(inviteError?.message ?? "Não foi possível enviar o convite.")}`);
    return;
  }

  const { error: insertProfileError } = await admin
    .from("profiles")
    .insert({ id: invited.user.id, organization_id: profile.organization_id, role });

  if (insertProfileError) {
    await admin.auth.admin.deleteUser(invited.user.id);
    redirect(`/team?error=${encodeURIComponent(insertProfileError.message)}`);
    return;
  }

  redirect("/team?invited=1");
}
