import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { addProductivityRule, updateProductivityRuleClassification, deleteProductivityRule } from "@/lib/productivityRules/actions";
import type { ProductivityRuleRow } from "@/lib/supabase/types";
import { PRODUCTIVITY_CLASS_LABELS, PRODUCTIVITY_CLASS_COLOR, type ProductivityClass } from "@/lib/dashboard/productivityClassification";

type SearchParams = Promise<{ error?: string }>;

const CLASS_OPTIONS: ProductivityClass[] = ["productive", "neutral", "unproductive"];

export default function ProductivityRulesPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto max-w-2xl space-y-6 p-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Regras de produtividade</h1>
          <p className="text-sm" style={{ color: "#898781" }}>
            Todo app novo visto num sync entra aqui como Produtivo — rebaixe para Neutro ou
            Improdutivo o que não for trabalho.
          </p>
        </div>
        <Link href="/" className="text-sm font-medium underline">
          Voltar ao dashboard
        </Link>
      </header>

      <Suspense fallback={<div className="h-64 animate-pulse rounded-lg bg-black/5" />}>
        <RulesData searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function RulesData({ searchParams }: { searchParams: SearchParams }) {
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

  const { data: ruleRows, error: rulesError } = await supabase
    .from("organization_productivity_rules")
    .select("*")
    .order("created_at", { ascending: false });

  if (rulesError) {
    return <p className="text-sm text-red-600">Erro ao carregar as regras: {rulesError.message}</p>;
  }

  const rules = (ruleRows ?? []) as ProductivityRuleRow[];
  const canManage = profile.role === "owner" || profile.role === "manager";

  return (
    <div className="space-y-4">
      {error && <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      {!canManage && (
        <p className="rounded-md p-3 text-sm" style={{ background: "#fdf3e1", color: "#6b4a06" }}>
          Só proprietários e gestores podem reclassificar, criar ou remover regras — você
          está vendo a lista como leitura.
        </p>
      )}

      <p className="text-xs" style={{ color: "#898781" }}>
        Essas regras têm prioridade sobre a classificação padrão do sistema. Um app sem
        nenhuma regra ainda (nunca sincronizado) conta como Neutro até aparecer aqui.
      </p>

      {rules.length === 0 ? (
        <p className="text-sm" style={{ color: "#898781" }}>
          Nenhum app sincronizado ainda — a lista se preenche sozinha conforme os
          dispositivos mandam dados.
        </p>
      ) : (
        <ul className="space-y-2">
          {rules.map((rule) => (
            <li
              key={rule.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm"
              style={{ borderColor: "#e1e0d9" }}
            >
              <div className="flex items-center gap-2">
                <span
                  className="inline-block h-2.5 w-2.5 rounded-full"
                  style={{ background: PRODUCTIVITY_CLASS_COLOR[rule.classification] }}
                  aria-hidden
                />
                <span className="font-medium">{rule.keyword}</span>
              </div>

              {canManage ? (
                <div className="flex items-center gap-3">
                  <form action={updateProductivityRuleClassification} className="flex items-center gap-2">
                    <input type="hidden" name="ruleId" value={rule.id} />
                    <select
                      name="classification"
                      defaultValue={rule.classification}
                      className="rounded-md border px-2 py-1 text-xs"
                      style={{ borderColor: "#e1e0d9" }}
                    >
                      {CLASS_OPTIONS.map((option) => (
                        <option key={option} value={option}>
                          {PRODUCTIVITY_CLASS_LABELS[option]}
                        </option>
                      ))}
                    </select>
                    <button type="submit" className="text-xs font-medium underline" style={{ color: "#52514e" }}>
                      Salvar
                    </button>
                  </form>
                  <form action={deleteProductivityRule}>
                    <input type="hidden" name="ruleId" value={rule.id} />
                    <button type="submit" className="text-xs font-medium underline" style={{ color: "#d03b3b" }}>
                      Remover
                    </button>
                  </form>
                </div>
              ) : (
                <span style={{ color: "#898781" }}>{PRODUCTIVITY_CLASS_LABELS[rule.classification]}</span>
              )}
            </li>
          ))}
        </ul>
      )}

      {canManage && (
        <form action={addProductivityRule} className="space-y-3 rounded-lg border p-4" style={{ borderColor: "#e1e0d9" }}>
          <h2 className="text-sm font-semibold">Nova regra manual</h2>
          <p className="text-xs" style={{ color: "#898781" }}>
            Apps já aparecem sozinhos acima — use isto para refinar por site dentro de um
            navegador (ex.: diferenciar uma aba do Chrome das outras pelo título da janela).
          </p>
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 space-y-1" style={{ minWidth: 180 }}>
              <label htmlFor="keyword" className="text-sm font-medium">
                Palavra-chave
              </label>
              <input
                id="keyword"
                name="keyword"
                type="text"
                required
                placeholder="ex.: receita federal, youtube"
                className="w-full rounded-md border px-3 py-2 text-sm"
                style={{ borderColor: "#e1e0d9" }}
              />
            </div>
            <div className="space-y-1">
              <label htmlFor="classification" className="text-sm font-medium">
                Classificação
              </label>
              <select
                id="classification"
                name="classification"
                defaultValue="productive"
                className="rounded-md border px-2 py-2 text-sm"
                style={{ borderColor: "#e1e0d9" }}
              >
                {CLASS_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {PRODUCTIVITY_CLASS_LABELS[option]}
                  </option>
                ))}
              </select>
            </div>
            <button
              type="submit"
              className="rounded-md px-3 py-2 text-sm font-medium text-white"
              style={{ background: "#0b0b0b" }}
            >
              Adicionar
            </button>
          </div>
          <p className="text-xs" style={{ color: "#898781" }}>
            A palavra-chave é procurada (sem diferenciar maiúsculas/minúsculas) dentro do
            nome do app + título da janela de cada evento.
          </p>
        </form>
      )}
    </div>
  );
}
