-- OneMonitor — schema de ingestão (Supabase/Postgres), Fases 4-5.
--
-- Escopo: receber os lotes (SyncBatch) que o agente desktop C# envia via
-- src/web-dashboard/app/api/sync/route.ts, incluindo os sinais de vida (status) da Fase 5.
-- Não há login de usuário final ainda (Fase 6) — por isso RLS fica ativado SEM policies
-- permissivas: só a service_role key (usada no servidor Next.js, nunca no navegador)
-- consegue ler/escrever. Isso bloqueia qualquer acesso anônimo por padrão, em vez de
-- liberar tudo até existir um dono de linha real.
--
-- Script idempotente: seguro rodar de novo em um banco onde uma versão anterior (Fase 4)
-- já rodou — ver a seção de migração perto do fim. Rode no SQL Editor do Supabase (ou via
-- `supabase db push`).

create extension if not exists "pgcrypto"; -- gen_random_uuid()

-- ---------------------------------------------------------------------------
-- devices: um registro por instalação do agente (DeviceId gerado e persistido
-- localmente em data/device-id.txt — ver DeviceIdentityProvider no agente).
-- ---------------------------------------------------------------------------
create table if not exists public.devices (
    id             uuid primary key default gen_random_uuid(),
    device_id      text not null unique,
    hostname       text,
    first_seen_at  timestamptz not null default now(),
    last_seen_at   timestamptz not null default now(),

    -- Fase 5: último sinal de vida autodeclarado pelo agente. 'offline' é o default de uma
    -- linha nova mas nunca é escrito pelo agente depois disso — ele só declara 'online'
    -- (Started/Heartbeat) e 'stopped' (encerrado pela bandeja); detectar "não dá heartbeat
    -- há muito tempo" é lógica de leitura futura (Fase 7), não um valor gravado aqui.
    status         text not null default 'offline' check (status in ('online', 'offline', 'stopped'))
);

comment on table public.devices is
    'Um registro por instalação do agente desktop (identificado pelo DeviceId persistido localmente).';

-- ---------------------------------------------------------------------------
-- usage_events: um registro por período de atividade/inatividade consolidado,
-- espelhando ActivitySyncEvent (src/desktop-agent/.../Core/Models/SyncModels.cs).
-- ---------------------------------------------------------------------------
create table if not exists public.usage_events (
    id                uuid primary key default gen_random_uuid(),

    -- Chave de deduplicação: o mesmo EventId gerado pelo agente na criação do registro
    -- (ActivityTracker.CreateRecord). Um reenvio (ex.: fusão de um bloco já sincronizado,
    -- ver SqliteActivityRepository) deve atualizar a linha, nunca duplicá-la.
    event_id          uuid not null unique,

    device_id         text not null references public.devices (device_id),

    started_at        timestamptz not null,
    ended_at          timestamptz not null,

    -- Gerada no banco: nunca confiar numa duração calculada e enviada pelo cliente.
    duration_seconds  integer generated always as
                           (greatest(0, extract(epoch from (ended_at - started_at))::integer)) stored,

    -- "Active" | "Idle" | "Unclassified" (ActivityType do agente, como texto — não está na
    -- lista original pedida, mas é indispensável: sem isso não dá pra separar tempo ativo
    -- de inativo no dashboard, nem saber que registros podem ter justificativa).
    activity_type     text not null check (activity_type in ('Active', 'Idle', 'Unclassified')),

    -- application_name hoje é um espelho de process_name (o agente não tem, ainda, uma
    -- tabela de nomes amigáveis tipo "chrome" -> "Google Chrome"); mantido como coluna
    -- própria porque o pedido da Fase 4 já antecipa esse enriquecimento futuro.
    application_name  text not null,
    process_name      text not null,
    window_title       text not null default '',

    category_name     text,
    justification      text,

    created_at        timestamptz not null default now(),
    updated_at        timestamptz not null default now(),

    constraint usage_events_time_order check (ended_at >= started_at)
);

comment on table public.usage_events is
    'Eventos de atividade/inatividade consolidados enviados em lote pelo agente desktop.';
comment on column public.usage_events.event_id is
    'Chave de deduplicação/upsert — mesmo valor de ActivityRecord.EventId no agente.';
comment on column public.usage_events.duration_seconds is
    'Calculada no banco (ended_at - started_at); nunca recebida do cliente.';

create index if not exists usage_events_device_started_idx
    on public.usage_events (device_id, started_at);
create index if not exists usage_events_started_at_idx
    on public.usage_events (started_at);
create index if not exists usage_events_category_idx
    on public.usage_events (category_name);

-- updated_at automático em upsert/update.
create or replace function public.set_updated_at()
returns trigger as $$
begin
    new.updated_at = now();
    return new;
end;
$$ language plpgsql;

drop trigger if exists usage_events_set_updated_at on public.usage_events;
create trigger usage_events_set_updated_at
    before update on public.usage_events
    for each row
    execute function public.set_updated_at();

-- ---------------------------------------------------------------------------
-- Row Level Security — ativado, sem policies permissivas (ver nota no topo).
-- ---------------------------------------------------------------------------
alter table public.devices enable row level security;
alter table public.usage_events enable row level security;

-- Nenhuma policy criada de propósito: por padrão, com RLS ativado e zero policies,
-- as roles anon/authenticated não conseguem ler nem escrever nada. Só a service_role key
-- (que ignora RLS por definição no Postgres/Supabase) acessa estas tabelas — e ela só deve
-- ser usada em código server-side (app/api/sync/route.ts, Server Components), nunca
-- exposta ao navegador. Quando a Fase 6 adicionar login, troque isto por policies que
-- filtrem por auth.uid()/dono do dispositivo.

-- ---------------------------------------------------------------------------
-- Migração Fase 5 — idempotente: se você já rodou este script antes da Fase 5
-- (devices sem a coluna status), isto preenche a lacuna sem recriar nada.
-- Rodar de novo depois que status já existe é um no-op seguro.
-- ---------------------------------------------------------------------------
alter table public.devices
    add column if not exists status text not null default 'offline';

alter table public.devices drop constraint if exists devices_status_check;
alter table public.devices
    add constraint devices_status_check check (status in ('online', 'offline', 'stopped'));

-- ---------------------------------------------------------------------------
-- Migração Fase 6 — Autenticação, multi-tenancy e gestão de organizações.
--
-- Até aqui (Fases 4-5) só existia a service_role key: um único "dono" de todos os
-- dispositivos/eventos, sem usuário final. A partir daqui cada organização tem sua própria
-- api_key (substituindo o SYNC_API_KEY global do agente) e seus próprios usuários
-- autenticados via Supabase Auth, enxergando só os dados da sua organização.
--
-- Idempotente: seguro rodar de novo num banco onde a Fase 6 já foi aplicada.
-- ---------------------------------------------------------------------------

create table if not exists public.organizations (
    id         uuid primary key default gen_random_uuid(),
    name       text not null,
    slug       text not null unique,
    api_key    text not null unique default encode(gen_random_bytes(32), 'hex'),
    created_at timestamptz not null default now()
);

comment on table public.organizations is
    'Uma organização (empresa/cliente) — dono de um conjunto de dispositivos e usuários.';
comment on column public.organizations.api_key is
    'Chave enviada pelo agente desktop (header X-Api-Key) para identificar a organização ao sincronizar — substitui o SYNC_API_KEY global das Fases 4-5.';

-- profiles: 1:1 com auth.users, carrega a organização e o papel do usuário dentro dela.
-- Não há FK para auth.users aqui por padrão de projeto Supabase (schema auth é gerenciado
-- pela plataforma), mas a própria auth.users garante a integridade de id via trigger
-- (ver bootstrap de cadastro no Next.js, que cria profiles só depois de criar o auth user).
create table if not exists public.profiles (
    id              uuid primary key references auth.users (id) on delete cascade,
    organization_id uuid not null references public.organizations (id) on delete cascade,
    role            text not null default 'owner' check (role in ('owner', 'manager', 'employee')),
    created_at      timestamptz not null default now()
);

comment on table public.profiles is
    'Vincula um usuário autenticado (auth.users) a uma organização e a um papel dentro dela.';

create index if not exists profiles_organization_idx on public.profiles (organization_id);

-- organization_id em devices/usage_events: nullable para não quebrar linhas que já existem
-- de instalações da Fase 4/5 (sem organização ainda) — passam a ser preenchidas a partir do
-- próximo sync, quando o agente enviar uma api_key de organização válida.
alter table public.devices
    add column if not exists organization_id uuid references public.organizations (id);
alter table public.usage_events
    add column if not exists organization_id uuid references public.organizations (id);

create index if not exists devices_organization_idx on public.devices (organization_id);
create index if not exists usage_events_organization_idx on public.usage_events (organization_id);

-- current_organization_id(): security definer para evitar recursão de RLS — uma policy em
-- profiles que fizesse "select organization_id from profiles where id = auth.uid()"
-- dispararia a própria RLS de profiles de novo. security definer executa com os
-- privilégios do dono da função (bypassa RLS internamente), então é seguro aqui: só
-- devolve a organização do usuário autenticado atual, nunca dados de terceiros.
create or replace function public.current_organization_id()
returns uuid
language sql
security definer
set search_path = public
stable
as $$
    select organization_id from public.profiles where id = auth.uid();
$$;

alter table public.organizations enable row level security;
alter table public.profiles enable row level security;

drop policy if exists organizations_select_own on public.organizations;
create policy organizations_select_own on public.organizations
    for select to authenticated
    using (id = public.current_organization_id());

drop policy if exists profiles_select_own_organization on public.profiles;
create policy profiles_select_own_organization on public.profiles
    for select to authenticated
    using (organization_id = public.current_organization_id());

drop policy if exists devices_select_own_organization on public.devices;
create policy devices_select_own_organization on public.devices
    for select to authenticated
    using (organization_id = public.current_organization_id());

drop policy if exists usage_events_select_own_organization on public.usage_events;
create policy usage_events_select_own_organization on public.usage_events
    for select to authenticated
    using (organization_id = public.current_organization_id());

-- Nenhuma policy de insert/update/delete para authenticated: o dashboard só lê. Escrita
-- continua exclusiva da service_role key (app/api/sync/route.ts e o bootstrap de cadastro),
-- que ignora RLS por definição.

-- ---------------------------------------------------------------------------
-- Migração Fase 8 — Configurações de organização (expediente, pausas, pop-up de
-- justificativa) e devolução delas ao agente desktop no heartbeat de sync.
--
-- Uma linha por organização (chave primária = organization_id, não um id próprio — não
-- há motivo para mais de uma configuração por organização). Lida pela tela /settings do
-- dashboard (RLS) e por app/api/sync/route.ts (service_role, para devolver ao agente).
--
-- Idempotente: seguro rodar de novo num banco onde a Fase 8 já foi aplicada.
-- ---------------------------------------------------------------------------

create table if not exists public.organization_settings (
    organization_id             uuid primary key references public.organizations (id) on delete cascade,
    work_start_time             time not null default '08:00',
    work_end_time                time not null default '18:00',
    mandatory_break_minutes     integer not null default 60,
    variable_break_minutes      integer not null default 0,
    enable_idle_popup           boolean not null default true,
    popup_idle_threshold_minutes integer not null default 15,
    updated_at                  timestamptz not null default now(),

    constraint organization_settings_work_hours_check check (work_end_time > work_start_time),
    constraint organization_settings_break_minutes_check
        check (mandatory_break_minutes >= 0 and variable_break_minutes >= 0),
    constraint organization_settings_popup_threshold_check check (popup_idle_threshold_minutes >= 0)
);

comment on table public.organization_settings is
    'Parâmetros de expediente/pausas e do pop-up de justificativa de ausência, por organização — ajustados em /settings (owner/manager) e devolvidos ao agente desktop no heartbeat de sync.';

drop trigger if exists organization_settings_set_updated_at on public.organization_settings;
create trigger organization_settings_set_updated_at
    before update on public.organization_settings
    for each row
    execute function public.set_updated_at();

alter table public.organization_settings enable row level security;

-- current_organization_role(): mesma técnica de current_organization_id() (security
-- definer, evita recursão) — devolve o papel do usuário autenticado atual, usado para
-- restringir quem pode editar as configurações (owner/manager, não employee).
create or replace function public.current_organization_role()
returns text
language sql
security definer
set search_path = public
stable
as $$
    select role from public.profiles where id = auth.uid();
$$;

drop policy if exists organization_settings_select_own on public.organization_settings;
create policy organization_settings_select_own on public.organization_settings
    for select to authenticated
    using (organization_id = public.current_organization_id());

drop policy if exists organization_settings_update_own on public.organization_settings;
create policy organization_settings_update_own on public.organization_settings
    for update to authenticated
    using (
        organization_id = public.current_organization_id()
        and public.current_organization_role() in ('owner', 'manager')
    )
    with check (
        organization_id = public.current_organization_id()
        and public.current_organization_role() in ('owner', 'manager')
    );

-- Backfill: toda organização criada antes desta fase (ou pelo bootstrap de cadastro, que
-- já passa a inserir sua própria linha) ganha uma linha de configurações com os padrões —
-- sem isso, /api/sync e a tela /settings não teriam nada para ler para organizações antigas.
insert into public.organization_settings (organization_id)
select o.id
from public.organizations o
left join public.organization_settings s on s.organization_id = o.id
where s.organization_id is null;
