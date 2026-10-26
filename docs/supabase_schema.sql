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
