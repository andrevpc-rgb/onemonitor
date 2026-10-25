import { Suspense } from "react";
import { connection } from "next/server";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import type { UsageEventRow } from "@/lib/supabase/types";
import { aggregateByApplication, aggregateByCategory, justifiedAbsences } from "@/lib/dashboard/aggregate";
import { hoursAgoIso } from "@/lib/dashboard/format";
import { assignCategoryColors } from "@/lib/viz/categoryColors";
import { AppUsageChart } from "@/components/AppUsageChart";
import { CategoryBreakdownChart } from "@/components/CategoryBreakdownChart";
import { JustifiedAbsencesTable } from "@/components/JustifiedAbsencesTable";

export default function DashboardPage() {
  return (
    <main className="mx-auto max-w-5xl space-y-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">OneMonitor — Dashboard</h1>
        <p className="text-sm" style={{ color: "#898781" }}>
          Últimas 24 horas, todos os dispositivos. (Visão única — sem login/seleção de
          dispositivo ainda; isso é Fase 5.)
        </p>
      </header>

      {/*
        A busca no Supabase precisa ficar num "buraco dinâmico" (Suspense) separado do
        shell estático: assim o `next build` consegue gerar a casca da página sem
        precisar de SUPABASE_URL/SUPABASE_SERVICE_ROLE_KEY reais, e os dados só são
        buscados de verdade quando a página é servida (ver Next.js 16 Cache Components).
      */}
      <Suspense fallback={<DashboardSkeleton />}>
        <DashboardData />
      </Suspense>
    </main>
  );
}

async function DashboardData() {
  // Marca explicitamente este componente como dependente de dados de requisição (Next.js
  // 16 Cache Components): sem isso, `next build` tenta executar a busca no Supabase
  // durante o prerender estático, mesmo dentro de um Suspense.
  await connection();

  const supabase = createSupabaseServerClient();
  const since = hoursAgoIso(24);

  const { data: events, error } = await supabase
    .from("usage_events")
    .select("*")
    .gte("started_at", since)
    .order("started_at", { ascending: true });

  if (error) {
    return <p className="text-sm text-red-600">Erro ao carregar dados: {error.message}</p>;
  }

  const rows = (events ?? []) as UsageEventRow[];
  const byApplication = aggregateByApplication(rows).slice(0, 10);
  const byCategory = aggregateByCategory(rows);
  const colorByCategory = assignCategoryColors(byCategory.map((c) => c.label));
  const absences = justifiedAbsences(rows);

  return (
    <>
      <AppUsageChart data={byApplication} />

      <CategoryBreakdownChart
        data={byCategory.map((c) => ({
          category: c.label,
          seconds: c.seconds,
          color: colorByCategory.get(c.label) ?? "var(--viz-series-8)",
        }))}
      />

      <JustifiedAbsencesTable data={absences} />
    </>
  );
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" aria-busy="true">
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-24 animate-pulse rounded-lg bg-black/5" />
      ))}
    </div>
  );
}
