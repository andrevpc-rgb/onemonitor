# Roadmap

## Fase 1 — Protótipo local (Windows) — ✅ concluída

- Detecção de aplicativo/processo/janela em primeiro plano.
- Duração de cada atividade.
- Detecção de inatividade (limite configurável, padrão 5 min).
- Agrupamento de períodos consecutivos da mesma atividade.
- Persistência local em SQLite, funcionamento 100% offline.
- Iniciar / pausar / encerrar monitoramento; visualizar status atual.
- Relatório diário local (HTML e JSON), separando tempo ativo / inativo / não classificado, com detalhamento por janela/site.
- Testes automatizados (agrupamento, duração, inatividade, persistência, recuperação após reinício).

Explicitamente **fora de escopo** na Fase 1 (não implementado): chamadas a Gemini/Claude/outras IAs, login, multiempresa, cobrança, sincronização com nuvem, capturas de tela, leitura de conteúdo de tela, execução automática de comandos, bloqueio de sites, integração com calendário, extensão de navegador.

## Fase 2 — Categorias e Justificativas — ✅ concluída

- Sistema de categorias configuráveis (processo/janela → categoria), com regras padrão em `categories.json`.
- Detecção do retorno do usuário após período de inatividade, com pop-up (WinForms) para justificar a ausência (ex.: "Reunião Presencial", "Suporte a Colega", "Visita a Cliente", "Pausa") — confirmado em teste manual.
- Persistência da justificativa associada ao período de inatividade correspondente, com migração automática de bancos criados antes dessa coluna existir.
- Relatório HTML/JSON com tempo por categoria, ausências justificadas (motivo, duração, ocorrências) e linha do tempo cronológica (horário local de início/fim de cada atividade), separados do tempo inativo genérico.
- Testes automatizados (resolução de categoria, evento de retorno de inatividade, persistência de justificativa, agregação no relatório, corte de dia pelo fuso local).

## Fase 3 — Sincronização e Comunicação com a Nuvem (lado do agente) — ✅ concluída

Escopo desta fase: preparar o **agente desktop** para enviar dados em lotes a um backend,
mantendo a resiliência offline do SQLite. Não inclui construir o backend/dashboard em si —
isso é trabalho futuro, quando houver um servidor real para apontar. Por isso a sincronização
vem **desabilitada por padrão** (`Sync.Enabled: false`); o agente continua 100% funcional
offline sem ela, como nas fases anteriores.

- Fila offline no SQLite: `ActivityRecords` ganha `EventId` (GUID, chave de deduplicação) e
  `SyncedAt` (nulo = pendente), com migração automática para bancos de fases anteriores.
- `SyncService` (Core, sem I/O): busca registros pendentes, monta lotes (`BatchSize`
  configurável) e usa `ISyncApiClient` para enviar — testável sem rede nem SQLite reais.
- Retentativa com backoff exponencial (1s, 2s, 4s...) para falhas transitórias (rede, 5xx);
  falhas permanentes (4xx) não insistem; em ambos os casos os registros ficam pendentes para
  o próximo ciclo, sem perda de dados.
- Autenticação por dispositivo: `DeviceId` (GUID persistido localmente) + `ApiKey` configurável
  em `appsettings.json`, enviados como headers HTTP.
- Contratos da API (`ActivitySyncEvent`, `SyncBatch`) em `Core/Models`, implementação HTTP
  (`HttpSyncApiClient`) na Infrastructure.
- Menu "Sincronizar agora" na bandeja (ativo só quando `Sync.Enabled`).
- Testes automatizados (fila/migração, montagem de lotes, retentativa com backoff mockando o
  HttpClient, marcação de sincronizado, reabertura de pendência ao fundir um registro já
  sincronizado).

Explicitamente fora do escopo desta fase: o backend/API em si, dashboard web, login de
usuário final — ficam para quando esse trabalho for decidido explicitamente.

## Fase 4 — Dashboard Web e Ingestão no Supabase — ✅ concluída

Escopo: backend de ingestão (Supabase/Postgres) + dashboard web de leitura, ambos sem
autenticação de usuário final ainda (isso é Fase 6) — visão única, não multi-tenant.
Deploy de produção no Vercel, validado ponta a ponta com dados reais.

- `docs/supabase_schema.sql`: tabelas `devices` e `usage_events` (schema Postgres), com
  `event_id` único para deduplicação (mesmo `EventId` que o agente já gera na Fase 3) e
  `duration_seconds` como coluna gerada (nunca confiar em duração calculada no cliente).
  RLS ativado nas duas tabelas, **sem policies permissivas** — só a `service_role key`
  (usada no servidor Next.js) acessa; acesso anônimo fica bloqueado até a Fase 6 definir
  policies por usuário.
- Extensão de contrato no agente: `ActivitySyncEvent` ganha `Category` (resolvida via o
  `ICategoryResolver` que já existia desde a Fase 2), para a tabela `usage_events` ter
  `category_name` sem duplicar a lógica de categorização em TypeScript.
- `src/web-dashboard/`: projeto Next.js (TypeScript + Tailwind + App Router), publicado em
  produção no Vercel.
  - `app/api/sync/route.ts`: endpoint de ingestão que recebe o `SyncBatch` do agente C#,
    valida a API key (header `X-Api-Key`), faz upsert em `devices`/`usage_events` via
    `@supabase/supabase-js` (service-role key, só no servidor) e devolve status HTTP
    alinhado ao que `HttpSyncApiClient` já espera (2xx sucesso, 4xx permanente, 5xx
    transitório) — nenhuma mudança necessária na classificação de erros do agente.
  - Dashboard (visão única, dia atual): gráfico de tempo ativo por aplicativo, distribuição
    de tempo por categoria e tabela de ausências justificadas.
  - Segredos (service-role key, API key de sync) nunca vão para o `appsettings.json`/`.env`
    versionado — `appsettings.Local.json` e `.env.local`, ambos gitignorados, são a forma
    correta de configurar uma instalação real (o repositório é público no GitHub).
- Publicação standalone do agente (`dotnet publish` self-contained, single-file) para
  instalar em outra máquina sem precisar do .NET SDK nem do código-fonte lá.

## Fase 5 — Autostart, Heartbeat e Alertas de Execução — ✅ concluída

Escopo: o agente passa a (a) poder iniciar sozinho com o Windows e (b) avisar o backend
quando está rodando — não só quando há atividade pra sincronizar. Sem isso, um agente que
trava, é fechado à força, ou cujo computador é desligado, fica indistinguível de um agente
que só não teve nada pra sincronizar; o backend não tem como saber se um dispositivo está
"vivo".

- Auto-inicialização com o Windows via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`,
  controlável por `Autostart.Enabled` em `appsettings.json` (aplicado/removido no início do
  agente, idempotente).
- Eventos de status enviados ao backend junto do ciclo de sincronização já existente:
  `Started` (uma vez, ao iniciar), `Heartbeat` (a cada ciclo periódico, mesmo sem atividade
  pendente — hoje o `SyncService` só chamava a API quando havia algo pra enviar), `Stopped`
  (ao encerrar pela bandeja, melhor esforço, com timeout curto pra não travar o fechamento).
- `devices` ganha uma coluna `status` (`online`/`stopped`; `offline` fica reservado pra uma
  lógica futura de "não dá heartbeat há muito tempo", calculada na leitura, não escrita pelo
  agente) atualizada pelo `/api/sync` quando o lote carrega um evento de status.
- Testes automatizados (controle de autostart contra o Registro real do Windows, montagem dos
  payloads de `Started`/`Heartbeat`/`Stopped`).

## Fase 6 — Autenticação, Multi-tenancy e Gestão de Organizações — ✅ concluída

Escopo: introduzir usuários reais (Supabase Auth) e isolar os dados por organização —
até aqui o dashboard mostrava uma visão única, global, de todos os dispositivos
(adequado só para um dono único validando a Fase 4/5 com seus próprios dados).

- `docs/supabase_schema.sql`: tabela `organizations` (id, name, slug, api_key, created_at) —
  `api_key` passa a ser por organização, substituindo o único `SYNC_API_KEY` global da Fase
  4/5. Tabela `profiles` (1:1 com `auth.users`, vinculada a `organization_id`, com `role`:
  `owner` | `manager` | `employee`). `organization_id` adicionado (nullable, para não quebrar
  linhas existentes) em `devices` e `usage_events`. Função `security definer`
  `current_organization_id()` para expor a organização do usuário autenticado às policies de
  RLS sem causar recursão ao consultar `profiles` dentro da própria policy de `profiles`.
  Novas policies de `select` para a role `authenticated`, filtrando por organização, nas
  quatro tabelas.
- `src/web-dashboard`: duas "personas" de cliente Supabase — `lib/supabase/admin.ts`
  (service-role, só em `/api/sync` e no bootstrap de cadastro) e `lib/supabase/server.ts`
  (sessão via cookies, `@supabase/ssr`, respeita RLS, usado pelo dashboard autenticado).
  Login/cadastro/logout usam Server Actions com formulários simples — não há necessidade de
  um client Supabase de navegador ainda (sem componente de cliente que precise dele hoje).
  - `middleware.ts`: renova a sessão e redireciona para `/login` quem não está autenticado.
  - `/login` e `/signup` (Server Actions): cadastro cria, numa única transação lógica, o
    usuário no `auth.users`, a organização e o `profile` com role `owner` (via client admin,
    que ignora RLS só para esse bootstrap).
  - `app/api/sync/route.ts`: em vez de validar contra um `SYNC_API_KEY` fixo no ambiente,
    resolve a organização pelo `api_key` recebido (lookup em `organizations`), grava
    `organization_id` em todo `device`/`usage_event` que ingere e rejeita (401) uma chave que
    não corresponda a nenhuma organização.
  - Dashboard (`app/page.tsx`): passa a usar o client de sessão (RLS já restringe à
    organização do usuário, sem filtro manual) e ganha um controle de logout.
- Fora de escopo explícito: convite de outros membros para a organização, troca de senha/
  recuperação de conta, e qualquer tela de gestão de papéis — a Fase 6 cria o modelo de
  dados e o fluxo mínimo de login/cadastro; a gestão fina de usuários fica para quando houver
  mais de uma organização real usando o sistema.
- Testes automatizados: suíte C# inalterada (contrato do agente não muda — a chave que ele já
  envia hoje simplesmente passa a ser resolvida por organização no servidor); verificação de
  tipos (`tsc --noEmit`) e lint (`next lint`) cobrindo os novos tipos/rotas de autenticação e
  organização no Next.js.

## Fase 7 — Dashboard B2B Avançado e Elementos Visuais para Gestão — ✅ concluída

Escopo: o dashboard deixa de ser uma visão única de "últimas 24h, todos os dispositivos" e
ganha filtros reais (período, dispositivo) e visualizações pensadas para gestão de equipe —
ainda sem um vínculo funcionário↔dispositivo (isso fica para uma fase futura; por ora cada
card representa um dispositivo).

- **Grid de dispositivos** (`DeviceStatusGrid`): um card por dispositivo da organização, com
  badge Online/Ausente/Offline derivado de `devices.status` + `last_seen_at` (não é mais só
  `status` cru — `status='stopped'` ou um `last_seen_at` muito antigo virar "Offline" é
  lógica de leitura, calculada aqui, nunca escrita pelo agente); busca por nome
  (client-side) e badge de produtividade por semáforo (verde ≥90% / amarelo 75-89% /
  vermelho <75%) do período filtrado.
- **Filtros** (`DashboardFilters`): período (Hoje / Ontem / Últimos 7 dias) e dispositivo
  específico, no topo da página, escopando tudo abaixo deles — grid incluso.
- **`ProductivityGauge`**: gauge (donut SVG, sem lib de gráficos) com a produtividade
  agregada da organização no período.
- **Linha do tempo diária** (`ActivityTimeline`): barra de 24h por dispositivo (small
  multiples), com blocos de Ativo/Inativo/Não classificado proporcionais ao horário real.
- **Produtividade** (`ProductivityBars`): Ativo vs. Inativo por dispositivo, com a Jornada
  Líquida Alvo da organização (expediente menos pausas) como referência.
- Distribuição por categoria e ausências justificadas: componentes da Fase 4 reaproveitados,
  agora escopados pelos novos filtros em vez de uma janela fixa de 24h.
- `lucide-react` adicionado para ícones dos badges de status e dos filtros — sem lib de
  gráficos nova (os gráficos continuam HTML/CSS puro, seguindo o skill de dataviz já usado
  desde a Fase 4). Telas/seções inspiradas num produto de referência mas que não tinham
  dado real no schema (férias, plano de carreira, carteira de clientes/honorário por setor,
  insights por IA) ficaram fora de propósito — ver Fase 9 para IA.
- `organization_settings`: expediente (`work_start_time`/`work_end_time`), pausas
  (`mandatory_break_minutes`/`variable_break_minutes`) e regras do pop-up de justificativa
  (`enable_idle_popup`/`popup_idle_threshold_minutes`), ajustados em `/settings`
  (owner/manager) e devolvidos ao agente no payload de resposta do heartbeat de sync.
  `ActivityTracker` (C#) corta um registro Idle exatamente no fim do expediente em vez de
  deixá-lo se estender até o retorno real do usuário, e `JustificationPolicy` só dispara o
  pop-up dentro do expediente, em dia de semana, acima do limiar configurado.

## Fase 8 — Auditoria, Gestão de Dispositivos e Retenção — ✅ concluída

Escopo: dar aos administradores da organização visibilidade sobre mudanças sensíveis e
controle sobre quem continua sincronizando — nenhuma das duas coisas existia desde a Fase 6
introduzir usuários/organizações reais.

- **`audit_logs`**: quem mudou o quê, gravado por **trigger de banco** (não depende de toda
  Server Action lembrar de logar) em `organization_settings` (qualquer campo alterado) e
  `devices` (revogação/restauração de acesso) — `actor_id` vem de `auth.uid()` no próprio
  request. RLS restringe leitura a `owner`/`manager`; tela `/audit` lista o histórico.
- **Revogação de dispositivo**: `devices.revoked_at`; quando preenchido, `/api/sync` rejeita
  (403) qualquer sync daquele dispositivo — nenhuma mudança necessária no agente C#, que já
  trata 4xx como falha permanente (sem retentativa) desde a Fase 3. Tela `/devices` lista os
  dispositivos da organização com um botão "Revogar/Restaurar acesso" (owner/manager).
- **Retenção**: `organization_settings.usage_events_retention_days` (padrão 180 dias),
  ajustável em `/settings`; expurgo automático via `pg_cron`
  (`purge_expired_usage_events()`, diário) em vez de uma rota HTTP dependente de um cron
  externo — mantém o expurgo dentro do próprio banco.
- Fora de escopo explícito: rotação de `api_key` por dispositivo (a chave continua por
  organização; revogação já cobre o caso de "parar de confiar numa máquina específica"),
  exportação de dados estilo LGPD self-service, e auditoria de ações de `employee` (hoje
  não há nenhuma ação sensível que um `employee` pratique).

## Hardening & Go-to-Market — ✅ concluída

Não é uma das 10 fases originais — um conjunto de melhorias para tornar o SaaS operável por
mais de uma pessoa/organização paga, feito logo após a Fase 8.

- **Convites de equipe**: `/team` (owner/manager convida por e-mail + papel). Usa
  `auth.admin.inviteUserByEmail` do próprio Supabase (único envio de e-mail já disponível
  no projeto — sem integração própria). `app/auth/callback` troca o `code` do link pela
  sessão; `/invite/set-password` conclui o cadastro. **Aviso operacional**: o e-mail do
  plano gratuito do Supabase é rate-limited e pensado para teste — convites em volume real
  exigem SMTP próprio configurado em Auth → Settings no painel do Supabase.
- **Rate limiting em `/api/sync`**: sem Redis/Upstash provisionado, o contador mora no
  próprio Postgres (`api_rate_limit_buckets` + `check_sync_rate_limit()`, janela de 1
  minuto, 20 req/dispositivo). Devolve 429, que `HttpSyncApiClient` já trata como falha
  transitória — nenhuma mudança no agente.
- **`/privacy` e `/terms`**: páginas públicas descrevendo o que é e o que não é coletado,
  linkadas no rodapé de `/login` e `/signup`. Deixam claro que são descrição técnica, não
  substituem revisão jurídica formal.

## Fase 9 — Insights com IA (futuro, não iniciado)

- IA aplicada **somente sobre dados já consolidados** (nunca em tempo real, nunca controlando mouse/teclado/computador): resumos, comparações, sugestões de produtividade.
- Nenhum uso de "computer use" como mecanismo de produto.

## Fase 10 — Extensões (futuro, não iniciado)

- Extensão de navegador (contexto complementar de abas/sites, com os mesmos princípios de privacidade).
- Integração com calendário.

Cada fase depende da anterior estar estável e será tratada como um projeto separado de implementação — este repositório não deve avançar para elas sem decisão explícita.
