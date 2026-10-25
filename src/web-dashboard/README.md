# OneMonitor — Dashboard Web

Fase 4 do projeto: backend de ingestão (Supabase/Postgres) + dashboard de leitura para os
dados que o [agente desktop](../desktop-agent/) envia. Next.js 16 (App Router, Cache
Components) + TypeScript + Tailwind CSS v4 + `@supabase/supabase-js`.

Sem login/multiusuário ainda (isso é a Fase 5) — é uma visão única, somando todos os
dispositivos, das últimas 24 horas.

## Pré-requisitos

- Node.js 20+ e npm.
- Um projeto Supabase já criado, com o schema de [`docs/supabase_schema.sql`](../../docs/supabase_schema.sql) aplicado (SQL Editor do Supabase, ou `supabase db push`).

### ⚠️ Rode `npm install` fora do Google Drive

Este repositório vive em `G:\Meu Drive\...` (unidade sincronizada pelo Google Drive) —
mesma limitação documentada no [README do agente desktop](../desktop-agent/../../README.md#limitação-conhecida-pasta-sincronizada-pelo-google-drive).
`npm install` grava milhares de arquivos pequenos em `node_modules/`, e o Google Drive
(modo stream) torna isso extremamente lento ou trava a instalação pela metade. Rode
`npm install` (e `npm run dev`) numa cópia local fora da sincronização, e só traga de volta
o código-fonte (nunca `node_modules/`, já ignorado pelo git) — ou mova o projeto para um
disco local se for trabalhar nele com frequência.

## Configuração

```bash
cp .env.local.example .env.local
```

Preencha `.env.local` com:

- `SUPABASE_URL` / `SUPABASE_SERVICE_ROLE_KEY` — Project Settings > API no painel do Supabase. A service-role key **nunca** deve ser exposta ao navegador (por isso não tem prefixo `NEXT_PUBLIC_`) — só é usada em Route Handlers e Server Components.
- `SYNC_API_KEY` — qualquer valor aleatório longo; precisa ser o **mesmo** valor configurado em `Sync.ApiKey` no `appsettings.json` do agente desktop.

## Scripts

```bash
npm install
npm run dev      # http://localhost:3000
npm run build    # build de produção
npm run lint
```

## Estrutura

```
app/
  page.tsx            # dashboard (Server Component; busca dinâmica via Suspense)
  api/sync/route.ts    # ingestão: recebe o SyncBatch do agente C#, valida a API key,
                        # faz upsert em devices/usage_events
components/            # AppUsageChart, CategoryBreakdownChart, JustifiedAbsencesTable
lib/
  supabase/            # client server-only (service-role) + tipos das tabelas
  sync/contracts.ts    # espelho TS do contrato SyncBatch/ActivitySyncEvent do C#
  dashboard/           # agregação (por app, por categoria, ausências justificadas)
  viz/                 # atribuição de cor categórica (paleta validada, ordem fixa)
```

## Notas técnicas

- **Next.js 16 Cache Components**: por padrão toda rota é estática a menos que marcada
  dinâmica. `app/page.tsx` isola a busca de dados num Server Component separado, dentro de
  um `<Suspense>`, com `await connection()` no topo — isso sinaliza explicitamente que
  depende de dados por requisição, permitindo que `next build` gere a casca estática da
  página sem precisar de credenciais reais do Supabase (o trecho dinâmico só roda quando a
  página é servida de verdade).
- **RLS**: o schema ativa Row Level Security sem nenhuma policy permissiva — só a
  service-role key (usada aqui, nunca no navegador) consegue ler/escrever. Isso é
  proposital enquanto não existe login (ver `docs/supabase_schema.sql`).
- **Contrato com o agente C#**: `lib/sync/contracts.ts` e `lib/supabase/types.ts` são
  mantidos manualmente em espelho com `OneMonitor.Agent.Core/Models/SyncModels.cs` — não há
  geração automática de tipos entre os dois ecossistemas. Se o contrato do agente mudar,
  atualize os dois lados.
