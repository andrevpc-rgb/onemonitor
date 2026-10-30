import { Suspense } from "react";
import { connection } from "next/server";
import Link from "next/link";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { logout } from "@/lib/auth/actions";
import type { DeviceRow, ProductivityRuleRow, UsageEventRow } from "@/lib/supabase/types";
import {
  aggregateActiveVsIdleByDevice,
  aggregateByApplication,
  aggregateByCategory,
  aggregateByProductivityClass,
  justifiedAbsences,
} from "@/lib/dashboard/aggregate";
import { isRangeKey, resolveDateRange, RANGE_OPTIONS, RANGE_DAY_COUNT, type RangeKey } from "@/lib/dashboard/dateRange";
import { DEFAULT_ORGANIZATION_SETTINGS, calculateNetTargetMinutes } from "@/lib/dashboard/workPolicy";
import { assignCategoryColors } from "@/lib/viz/categoryColors";
import { formatRelativeTime, formatDateTime } from "@/lib/dashboard/format";
import { DeviceStatusGrid } from "@/components/DeviceStatusGrid";
import { DashboardFilters } from "@/components/DashboardFilters";
import { ProductivityGauge } from "@/components/ProductivityGauge";
import { ActivityTimeline } from "@/components/ActivityTimeline";
import { ProductivityBars } from "@/components/ProductivityBars";
import { AppUsageChart } from "@/components/AppUsageChart";
import { CategoryBreakdownChart } from "@/components/CategoryBreakdownChart";
import { JustifiedAbsencesTable } from "@/components/JustifiedAbsencesTable";

type SearchParams = Promise<{ range?: string; device?: string }>;

export default function DashboardPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <main className="mx-auto max-w-5xl space-y-6 p-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">OneMonitor — Dashboard</h1>
          <p className="text-sm" style={{ color: "#898781" }}>
            Visão da sua organização, com filtro por período e dispositivo.
          </p>
        </div>
        <div className="flex items-center gap-4">
          <Link href="/team" className="text-sm font-medium underline">
            Equipe
          </Link>
          <Link href="/devices" className="text-sm font-medium underline">
            Dispositivos
          </Link>
          <Link href="/audit" className="text-sm font-medium underline">
            Auditoria
          </Link>
          <Link href="/productivity-rules" className="text-sm font-medium underline">
            Regras de produtividade
          </Link>
          <Link href="/settings" className="text-sm font-medium underline">
            Configurações
          </Link>
          <form action={logout}>
            <button type="submit" className="text-sm font-medium underline">
              Sair
            </button>
          </form>
        </div>
      </header>

      {/*
        A busca no Supabase precisa ficar num "buraco dinâmico" (Suspense) separado do
        shell estático: assim o `next build` consegue gerar a casca da página sem
        precisar de credenciais reais nem de uma sessão de usuário, e os dados só são
        buscados de verdade quando a página é servida (ver Next.js 16 Cache Components).
      */}
      <Suspense fallback={<DashboardSkeleton />}>
        <DashboardData searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function DashboardData({ searchParams }: { searchParams: SearchParams }) {
  // Marca explicitamente este componente como dependente de dados de requisição (Next.js
  // 16 Cache Components): sem isso, `next build` tenta executar a busca no Supabase
  // durante o prerender estático, mesmo dentro de um Suspense.
  await connection();

  const params = await searchParams;
  const range: RangeKey = isRangeKey(params.range) ? params.range : "today";
  const selectedDevice = params.device && params.device !== "all" ? params.device : undefined;

  const now = new Date();
  const { sinceIso, untilIso, timelineDayStartIso } = resolveDateRange(range, now);

  const supabase = await createSupabaseServerClient();

  // Sem filtro manual por organização em nenhuma consulta abaixo: a policy de RLS de cada
  // tabela (docs/supabase_schema.sql) já restringe o resultado à organização do usuário.
  const { data: deviceRows, error: devicesError } = await supabase
    .from("devices")
    .select("*")
    .order("hostname", { ascending: true });

  if (devicesError) {
    return <p className="text-sm text-red-600">Erro ao carregar dispositivos: {devicesError.message}</p>;
  }

  const devices = (deviceRows ?? []) as DeviceRow[];

  let mainEventsQuery = supabase
    .from("usage_events")
    .select("*")
    .gte("started_at", sinceIso)
    .lt("started_at", untilIso)
    .order("started_at", { ascending: true });
  if (selectedDevice) {
    mainEventsQuery = mainEventsQuery.eq("device_id", selectedDevice);
  }

  const { data: mainEventRows, error: eventsError } = await mainEventsQuery;

  if (eventsError) {
    return <p className="text-sm text-red-600">Erro ao carregar eventos: {eventsError.message}</p>;
  }

  const mainEvents = (mainEventRows ?? []) as UsageEventRow[];

  // "Últimos 7 dias" cobre uma janela maior que um único dia — a linha do tempo diária
  // precisa de uma busca própria, só para o dia que ela exibe (ver resolveDateRange).
  let timelineEvents = mainEvents;
  if (range === "7d") {
    const timelineDayEndIso = new Date(new Date(timelineDayStartIso).getTime() + 24 * 60 * 60 * 1000).toISOString();
    let timelineQuery = supabase
      .from("usage_events")
      .select("*")
      .gte("started_at", timelineDayStartIso)
      .lt("started_at", timelineDayEndIso)
      .order("started_at", { ascending: true });
    if (selectedDevice) {
      timelineQuery = timelineQuery.eq("device_id", selectedDevice);
    }

    const { data: timelineRows, error: timelineError } = await timelineQuery;
    if (timelineError) {
      return <p className="text-sm text-red-600">Erro ao carregar a linha do tempo: {timelineError.message}</p>;
    }
    timelineEvents = (timelineRows ?? []) as UsageEventRow[];
  }

  const filteredDevices = selectedDevice ? devices.filter((d) => d.device_id === selectedDevice) : devices;

  const { data: ruleRows } = await supabase.from("organization_productivity_rules").select("*");
  const productivityRules = ((ruleRows ?? []) as ProductivityRuleRow[]).map((r) => ({
    keyword: r.keyword,
    classification: r.classification,
  }));

  const byApplication = aggregateByApplication(mainEvents, productivityRules).slice(0, 10);
  const byCategory = aggregateByCategory(mainEvents);
  const colorByCategory = assignCategoryColors(byCategory.map((c) => c.label));
  const absences = justifiedAbsences(mainEvents);
  const productivity = aggregateActiveVsIdleByDevice(mainEvents, filteredDevices);

  // Uma linha só por organização (RLS já restringe a own org — ver docs/supabase_schema.sql).
  const { data: settingsRow } = await supabase.from("organization_settings").select("*").maybeSingle();
  const netTargetMinutes = calculateNetTargetMinutes(settingsRow ?? DEFAULT_ORGANIZATION_SETTINGS);

  const periodLabel = RANGE_OPTIONS.find((o) => o.key === range)?.label ?? "Período selecionado";
  const { productiveSeconds, neutralSeconds, unproductiveSeconds } = aggregateByProductivityClass(mainEvents, productivityRules);
  // Jornada líquida é "por dispositivo, por dia" — multiplica pelo número de dispositivos
  // no escopo filtrado (a meta cresce junto com a equipe) e pelos dias cobertos pelo
  // período (sem isso, "Últimos 7 dias" bateria 100% trivialmente contra a meta de 1 dia só).
  const targetSeconds = netTargetMinutes * 60 * filteredDevices.length * RANGE_DAY_COUNT[range];

  // Mais recente last_seen_at entre os dispositivos no escopo filtrado — um "dado atualizado
  // até" discreto, não repete o "Visto Xmin atrás" de cada card (esse é por dispositivo).
  const lastUpdateIso = filteredDevices.reduce<string | null>(
    (latest, d) => (!latest || d.last_seen_at > latest ? d.last_seen_at : latest),
    null,
  );

  return (
    <>
      <DashboardFilters devices={devices} selectedRange={range} selectedDevice={selectedDevice} />

      {lastUpdateIso && (
        <p className="text-xs" style={{ color: "#898781" }} title={formatDateTime(lastUpdateIso)}>
          Dados atualizados {formatRelativeTime(lastUpdateIso, now)}
        </p>
      )}

      <DeviceStatusGrid devices={filteredDevices} now={now} productivity={productivity} periodLabel={periodLabel} />

      <ProductivityGauge
        productiveSeconds={productiveSeconds}
        neutralSeconds={neutralSeconds}
        unproductiveSeconds={unproductiveSeconds}
        targetSeconds={targetSeconds}
        periodLabel={periodLabel}
      />

      <ActivityTimeline devices={filteredDevices} events={timelineEvents} dayStartIso={timelineDayStartIso} />

      <ProductivityBars data={productivity} targetMinutes={netTargetMinutes} />

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
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="h-24 animate-pulse rounded-lg bg-black/5" />
      ))}
    </div>
  );
}
