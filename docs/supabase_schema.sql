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

-- ---------------------------------------------------------------------------
-- Migração Fase 8 — Auditoria, Gestão/Revogação de Dispositivos e Retenção.
--
-- Três peças independentes:
-- 1) audit_logs: quem mudou o quê (configurações da organização, revogação de
--    dispositivo), gravado por trigger de banco (não depende de toda Server Action
--    lembrar de logar) — só owner/manager leem.
-- 2) devices.revoked_at: um dispositivo revogado é rejeitado (403) pelo /api/sync —
--    nenhuma mudança necessária no agente C#, que já trata 4xx como falha permanente.
-- 3) organization_settings.usage_events_retention_days + expurgo automático via pg_cron.
--
-- Idempotente: seguro rodar de novo num banco onde a Fase 8 já foi aplicada.
-- ---------------------------------------------------------------------------

-- --- 1) Auditoria -----------------------------------------------------------

create table if not exists public.audit_logs (
    id              uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,

    -- Quem autenticado fez a mudança — null para mudanças feitas pelo service_role (ex.:
    -- nenhuma hoje, mas um script administrativo futuro cairia aqui) ou se o usuário foi
    -- removido depois (on delete set null: o log fica, só perde o vínculo com a pessoa).
    actor_id        uuid references auth.users (id) on delete set null,

    action          text not null,
    target_type     text not null,
    target_id       text,
    changes         jsonb,
    created_at      timestamptz not null default now()
);

comment on table public.audit_logs is
    'Trilha de auditoria — quem mudou o quê, gravada por trigger de banco em organization_settings e devices. Só owner/manager leem.';

create index if not exists audit_logs_organization_created_idx
    on public.audit_logs (organization_id, created_at desc);

alter table public.audit_logs enable row level security;

drop policy if exists audit_logs_select_own_organization on public.audit_logs;
create policy audit_logs_select_own_organization on public.audit_logs
    for select to authenticated
    using (
        organization_id = public.current_organization_id()
        and public.current_organization_role() in ('owner', 'manager')
    );

-- Nenhuma policy de insert/update/delete para authenticated: só os triggers abaixo (security
-- definer, portanto gravam independente de RLS) escrevem nesta tabela.

create or replace function public.log_organization_settings_audit()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.audit_logs (organization_id, actor_id, action, target_type, target_id, changes)
    values (
        new.organization_id,
        auth.uid(),
        'organization_settings.updated',
        'organization_settings',
        new.organization_id::text,
        jsonb_build_object('old', to_jsonb(old), 'new', to_jsonb(new))
    );
    return new;
end;
$$;

drop trigger if exists organization_settings_audit on public.organization_settings;
create trigger organization_settings_audit
    after update on public.organization_settings
    for each row
    execute function public.log_organization_settings_audit();

create or replace function public.log_device_revocation_audit()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    -- organization_id pode ser null em dispositivos de instalações anteriores à Fase 6
    -- que nunca sincronizaram de novo com uma api_key válida — audit_logs exige uma
    -- organização, então esses ficam de fora do log (não há "dono" para atribuir o evento).
    if new.organization_id is not null and new.revoked_at is distinct from old.revoked_at then
        insert into public.audit_logs (organization_id, actor_id, action, target_type, target_id, changes)
        values (
            new.organization_id,
            auth.uid(),
            case when new.revoked_at is not null then 'device.revoked' else 'device.restored' end,
            'device',
            new.device_id,
            jsonb_build_object('old_revoked_at', old.revoked_at, 'new_revoked_at', new.revoked_at)
        );
    end if;
    return new;
end;
$$;

drop trigger if exists devices_revocation_audit on public.devices;
create trigger devices_revocation_audit
    after update on public.devices
    for each row
    execute function public.log_device_revocation_audit();

-- --- 2) Gestão/revogação de dispositivos ------------------------------------

alter table public.devices add column if not exists revoked_at timestamptz;

comment on column public.devices.revoked_at is
    'Quando preenchido, /api/sync passa a rejeitar (403) qualquer sync deste dispositivo — ver app/api/sync/route.ts. Alternado em /devices (owner/manager).';

drop policy if exists devices_update_own_organization on public.devices;
create policy devices_update_own_organization on public.devices
    for update to authenticated
    using (
        organization_id = public.current_organization_id()
        and public.current_organization_role() in ('owner', 'manager')
    )
    with check (
        organization_id = public.current_organization_id()
        and public.current_organization_role() in ('owner', 'manager')
    );

-- --- 3) Retenção e expurgo automático ---------------------------------------

alter table public.organization_settings
    add column if not exists usage_events_retention_days integer not null default 180;

alter table public.organization_settings drop constraint if exists organization_settings_retention_check;
alter table public.organization_settings
    add constraint organization_settings_retention_check check (usage_events_retention_days > 0);

-- Em alguns projetos Supabase, habilitar pg_cron pela primeira vez exige um clique em
-- Database > Extensions (a role usada pelo SQL Editor às vezes não tem o privilégio direto
-- via "create extension"). Se esta linha falhar, habilite por lá e rode o script de novo.
create extension if not exists pg_cron;

create or replace function public.purge_expired_usage_events()
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    delete from public.usage_events ue
    using public.organization_settings os
    where ue.organization_id = os.organization_id
      and ue.started_at < now() - (os.usage_events_retention_days || ' days')::interval;

    -- usage_events sem organization_id (instalações anteriores à Fase 6) não têm uma linha
    -- de organization_settings para consultar o retention_days — ficam de fora do expurgo
    -- automático de propósito, em vez de assumir um padrão arbitrário para esses dados.
end;
$$;

comment on function public.purge_expired_usage_events() is
    'Apaga usage_events mais antigos que organization_settings.usage_events_retention_days de cada organização. Agendada via pg_cron, 03:00 UTC diariamente.';

do $$
begin
    if exists (select 1 from cron.job where jobname = 'purge-expired-usage-events') then
        perform cron.unschedule('purge-expired-usage-events');
    end if;
end;
$$;

select cron.schedule(
    'purge-expired-usage-events',
    '0 3 * * *',
    $$select public.purge_expired_usage_events();$$
);

-- ---------------------------------------------------------------------------
-- Migração "Hardening" — Rate limiting de /api/sync.
--
-- Vercel roda cada request numa função serverless potencialmente nova, então um contador
-- em memória não sobreviveria entre requisições — por isso o contador mora no Postgres
-- (já provisionado) em vez de um serviço novo (Redis/Upstash) que o projeto não tem.
--
-- Sem RLS com policies permissivas de propósito (mesmo padrão de devices/usage_events nas
-- Fases 4/6): só o service_role (via check_sync_rate_limit, chamada por
-- app/api/sync/route.ts) ou a própria função (security definer) tocam esta tabela.
--
-- Idempotente: seguro rodar de novo num banco onde esta migração já foi aplicada.
-- ---------------------------------------------------------------------------

create table if not exists public.api_rate_limit_buckets (
    device_id      text not null,
    window_start   timestamptz not null,
    request_count  integer not null default 0,
    primary key (device_id, window_start)
);

comment on table public.api_rate_limit_buckets is
    'Contador de requisições por dispositivo e janela de 1 minuto, usado por check_sync_rate_limit() para proteger /api/sync de sobrecarga.';

alter table public.api_rate_limit_buckets enable row level security;

create or replace function public.check_sync_rate_limit(p_device_id text, p_max_requests integer default 20)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
    v_window timestamptz := date_trunc('minute', now());
    v_count  integer;
begin
    insert into public.api_rate_limit_buckets (device_id, window_start, request_count)
    values (p_device_id, v_window, 1)
    on conflict (device_id, window_start)
        do update set request_count = api_rate_limit_buckets.request_count + 1
    returning request_count into v_count;

    -- Limpeza oportunista de janelas antigas a cada chamada — evita crescimento indefinido
    -- da tabela sem precisar de um segundo job de pg_cron só para isso.
    delete from public.api_rate_limit_buckets where window_start < v_window - interval '10 minutes';

    return v_count <= p_max_requests;
end;
$$;

comment on function public.check_sync_rate_limit(text, integer) is
    'Incrementa o contador do dispositivo na janela de 1 minuto corrente e devolve se ainda está dentro do limite (default 20 req/min). Chamada por app/api/sync/route.ts antes de processar o lote.';
