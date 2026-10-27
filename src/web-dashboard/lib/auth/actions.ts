"use server";

import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseAdminClient } from "@/lib/supabase/admin";
import crypto from "node:crypto";

export async function login(formData: FormData) {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  const supabase = await createSupabaseServerClient();
  const { error } = await supabase.auth.signInWithPassword({ email, password });

  if (error) {
    redirect(`/login?error=${encodeURIComponent(error.message)}`);
  }

  redirect("/");
}

export async function logout() {
  const supabase = await createSupabaseServerClient();
  await supabase.auth.signOut();
  redirect("/login");
}

/**
 * Cadastro: cria, nessa ordem, o usuário no auth.users (client admin — ainda não há
 * sessão), a organização e o profile com role "owner". Não é uma transação de banco de
 * verdade (auth.users e as tabelas do schema público não compartilham uma transação no
 * Supabase hosted), então, se a organização ou o profile falharem depois de criado o
 * usuário, desfazemos manualmente apagando o usuário recém-criado — melhor do que deixar
 * um auth.users órfão, sem profile, que nunca conseguiria logar em organização nenhuma.
 */
export async function signup(formData: FormData) {
  const organizationName = String(formData.get("organizationName") ?? "").trim();
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  if (!organizationName || !email || !password) {
    redirect(`/signup?error=${encodeURIComponent("Preencha todos os campos.")}`);
  }

  const admin = createSupabaseAdminClient();

  const { data: created, error: createUserError } = await admin.auth.admin.createUser({
    email,
    password,
    email_confirm: true,
  });

  if (createUserError || !created.user) {
    redirect(`/signup?error=${encodeURIComponent(createUserError?.message ?? "Não foi possível criar o usuário.")}`);
    return;
  }

  const userId = created.user.id;

  const { data: organization, error: organizationError } = await admin
    .from("organizations")
    .insert({ name: organizationName, slug: slugify(organizationName) })
    .select("id")
    .single();

  if (organizationError || !organization) {
    await admin.auth.admin.deleteUser(userId);
    redirect(`/signup?error=${encodeURIComponent(organizationError?.message ?? "Não foi possível criar a organização.")}`);
    return;
  }

  const { error: profileError } = await admin
    .from("profiles")
    .insert({ id: userId, organization_id: organization.id, role: "owner" });

  if (profileError) {
    await admin.auth.admin.deleteUser(userId);
    redirect(`/signup?error=${encodeURIComponent(profileError.message)}`);
    return;
  }

  const supabase = await createSupabaseServerClient();
  const { error: signInError } = await supabase.auth.signInWithPassword({ email, password });

  if (signInError) {
    redirect("/login");
    return;
  }

  redirect("/");
}

function slugify(name: string): string {
  const base = name
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "") // remove acentos (marcas diacríticas combinantes pós-NFD)
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");

  // Sufixo curto e aleatório: evita colisão de slug entre organizações com nomes iguais
  // sem precisar de uma tela de "esse nome já existe, escolha outro".
  return `${base || "organizacao"}-${crypto.randomBytes(3).toString("hex")}`;
}
