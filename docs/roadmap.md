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
autenticação de usuário final ainda (isso é Fase 5) — visão única, não multi-tenant.

- `docs/supabase_schema.sql`: tabelas `devices` e `usage_events` (schema Postgres), com
  `event_id` único para deduplicação (mesmo `EventId` que o agente já gera na Fase 3) e
  `duration_seconds` como coluna gerada (nunca confiar em duração calculada no cliente).
  RLS ativado nas duas tabelas, **sem policies permissivas** — só a `service_role key`
  (usada no servidor Next.js) acessa; acesso anônimo fica bloqueado até a Fase 5 definir
  policies por usuário.
- Extensão de contrato no agente: `ActivitySyncEvent` ganha `Category` (resolvida via o
  `ICategoryResolver` que já existia desde a Fase 2), para a tabela `usage_events` ter
  `category_name` sem duplicar a lógica de categorização em TypeScript.
- `src/web-dashboard/`: projeto Next.js (TypeScript + Tailwind + App Router).
  - `app/api/sync/route.ts`: endpoint de ingestão que recebe o `SyncBatch` do agente C#,
    valida a API key (header `X-Api-Key`), faz upsert em `devices`/`usage_events` via
    `@supabase/supabase-js` (service-role key, só no servidor) e devolve status HTTP
    alinhado ao que `HttpSyncApiClient` já espera (2xx sucesso, 4xx permanente, 5xx
    transitório) — nenhuma mudança necessária na classificação de erros do agente.
  - Dashboard (visão única, dia atual): gráfico de tempo ativo por aplicativo, distribuição
    de tempo por categoria e tabela de ausências justificadas.

## Fase 5 — Autenticação, Multiempresa e Gestão (futuro, não iniciado)

- Login de usuário final; policies de RLS por usuário/dispositivo no Supabase.
- Multi-tenancy (empresas, equipes, gestores), papéis e permissões (usuário vs. gestor).
- Seletor de dispositivo/período no dashboard (hoje ele mostra uma visão única do dia atual).
- Cobrança/planos.

## Fase 6 — Insights com IA (futuro, não iniciado)

- IA aplicada **somente sobre dados já consolidados** (nunca em tempo real, nunca controlando mouse/teclado/computador): resumos, comparações, sugestões de produtividade.
- Nenhum uso de "computer use" como mecanismo de produto.

## Fase 7 — Extensões (futuro, não iniciado)

- Extensão de navegador (contexto complementar de abas/sites, com os mesmos princípios de privacidade).
- Integração com calendário.

Cada fase depende da anterior estar estável e será tratada como um projeto separado de implementação — este repositório não deve avançar para elas sem decisão explícita.
