"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import type { ProductivityClass } from "@/lib/dashboard/productivityClassification";

const VALID_CLASSES: ProductivityClass[] = ["productive", "neutral", "unproductive"];

/**
 * Client de sessão (não admin): as policies de insert/delete de
 * organization_productivity_rules (RLS, ver docs/supabase_schema.sql) já exigem
 * owner/manager da própria organização — reforça no banco a mesma regra que a tela
 * /productivity-rules aplica na UI.
 */
export async function addProductivityRule(formData: FormData) {
  const keyword = String(formData.get("keyword") ?? "").trim();
  const classification = String(formData.get("classification") ?? "");

  if (!keyword || !VALID_CLASSES.includes(classification as ProductivityClass)) {
    redirect(`/productivity-rules?error=${encodeURIComponent("Preencha uma palavra-chave e uma classificação válidas.")}`);
    return;
  }

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
    redirect(`/productivity-rules?error=${encodeURIComponent("Perfil não encontrado.")}`);
    return;
  }

  const { error } = await supabase
    .from("organization_productivity_rules")
    .insert({ organization_id: profile.organization_id, keyword, classification });

  if (error) {
    const message = error.code === "23505" ? `Já existe uma regra para "${keyword}".` : error.message;
    redirect(`/productivity-rules?error=${encodeURIComponent(message)}`);
    return;
  }

  revalidatePath("/productivity-rules");
  redirect("/productivity-rules");
}

/** Reclassifica uma regra existente (ex.: as que entraram sozinhas como "productive" via
 * descoberta automática no /api/sync) sem precisar remover e recriar. */
export async function updateProductivityRuleClassification(formData: FormData) {
  const ruleId = String(formData.get("ruleId") ?? "");
  const classification = String(formData.get("classification") ?? "");

  if (!VALID_CLASSES.includes(classification as ProductivityClass)) {
    redirect(`/productivity-rules?error=${encodeURIComponent("Classificação inválida.")}`);
    return;
  }

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { error } = await supabase
    .from("organization_productivity_rules")
    .update({ classification })
    .eq("id", ruleId);

  if (error) {
    redirect(`/productivity-rules?error=${encodeURIComponent(error.message)}`);
    return;
  }

  revalidatePath("/productivity-rules");
  redirect("/productivity-rules");
}

export async function deleteProductivityRule(formData: FormData) {
  const ruleId = String(formData.get("ruleId") ?? "");

  const supabase = await createSupabaseServerClient();
  const { data: userData } = await supabase.auth.getUser();
  if (!userData.user) {
    redirect("/login");
    return;
  }

  const { error } = await supabase.from("organization_productivity_rules").delete().eq("id", ruleId);

  if (error) {
    redirect(`/productivity-rules?error=${encodeURIComponent(error.message)}`);
    return;
  }

  revalidatePath("/productivity-rules");
  redirect("/productivity-rules");
}
