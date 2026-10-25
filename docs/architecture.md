# Arquitetura — Agente Desktop

## Visão geral

O agente é um processo WinForms em segundo plano (sem janela principal, apenas ícone na bandeja) que, em um intervalo configurável (padrão 2s), faz o seguinte laço:

1. Lê o tempo de inatividade do sistema (`GetLastInputInfo`).
2. Se abaixo do limite de inatividade, lê a janela em primeiro plano (`GetForegroundWindow` + `GetWindowText` + `GetWindowThreadProcessId` → nome do processo).
3. Classifica o instante como `Active`, `Idle` ou `Unclassified` (quando não foi possível determinar a janela/processo, ex.: tela de bloqueio, UAC).
4. Atualiza ou fecha o registro de atividade corrente e persiste no SQLite.

Não há hooks globais de teclado/mouse e não há captura de tela — ver [privacy.md](privacy.md) para o porquê.

## Camadas

```
OneMonitor.Agent (net8.0-windows, WinForms)
  └─ depende de → Core e Infrastructure
       (composição: Program.cs monta as dependências e inicia o TrayApplicationContext)

OneMonitor.Agent.Infrastructure (net8.0-windows)
  └─ depende de → Core
       P/Invoke Win32 (user32.dll), SQLite (Microsoft.Data.Sqlite), geração de arquivos de relatório

OneMonitor.Agent.Core (net8.0, sem dependência de Windows)
       Modelo de domínio, interfaces (Abstractions) e a máquina de estados do monitoramento
       100% testável sem Windows real, via fakes das interfaces
```

A separação Core/Infrastructure existe para que a lógica de negócio (classificação, agrupamento, cálculo de duração, geração de relatório) seja testável em isolamento e para que fases futuras (sincronização, dashboard) possam reutilizar o `Core` sem herdar dependências de Windows.

## Máquina de estados (`ActivityTracker`)

`OneMonitor.Agent.Core.Services.ActivityTracker` mantém um único "registro aberto" em memória:

- Em cada `Tick()`, calcula a classificação atual (processo, título, tipo).
- Se é igual à do registro aberto (mesmo processo + título + tipo) → apenas estende `EndTime`. É assim que períodos consecutivos da mesma atividade são **agrupados** sem gerar uma linha por tick.
- Se é diferente → fecha o registro aberto (persiste via `IActivityRepository`) e abre um novo com `StartTime = agora`.
- `Flush()` força o fechamento/persistência do registro aberto (usado ao pausar/encerrar).

Essa classe depende apenas de interfaces (`IActiveWindowProvider`, `IIdleTimeProvider`, `IActivityRepository`, `IClock`), o que permite testá-la com um relógio e providers falsos, sem depender de tempo real ou do Windows.

## Agrupamento na persistência (`SqliteActivityRepository`)

Além do agrupamento em memória, o repositório funde no banco um registro novo com o último registro gravado quando ambos têm o mesmo processo/título/tipo **e** são contíguos no tempo (`last.EndTime == novo.StartTime`). Isso cobre o caso de retomada dentro da mesma sessão de processo; após um reinício real do agente há uma lacuna de tempo, então um novo registro é criado corretamente em vez de fundido — é esse comportamento que os testes de "recuperação após reinício" verificam.

## Detecção de inatividade

`IdleTimeProvider` usa `GetLastInputInfo` (retorna o tick do último input de teclado/mouse, sem qualquer informação sobre qual tecla/botão) e compara com `Environment.TickCount`, usando aritmética sem sinal (`uint`) para que o cálculo continue correto mesmo quando o contador do Windows "dá a volta" (a cada ~49.7 dias de uptime). O limite de inatividade é configurável (`appsettings.json`, padrão 5 minutos).

## Persistência

SQLite via `Microsoft.Data.Sqlite` (sem ORM — o schema é pequeno e as queries são poucas, não há necessidade de EF Core nesta fase). Tabela única `ActivityRecords` com índice implícito pela chave primária `Id`. Caminho do banco:

- **Ambiente de desenvolvimento** (checkout com `OneMonitor.Agent.sln` localizável a partir do executável): `./data/onemonitor.db` na raiz do repositório.
- **Fora de um checkout de desenvolvimento** (ou se detecção falhar): `%LOCALAPPDATA%\OneMonitor\onemonitor.db`.
- Sempre sobrescrevível via `appsettings.json` (`DatabasePath`).

`SqliteActivityRepository.Initialize()` roda `CREATE TABLE IF NOT EXISTS` e, em seguida, verifica via `PRAGMA table_info` se a coluna `Justification` (adicionada na Fase 2) existe; se não existir — caso de um banco criado antes dela —, roda `ALTER TABLE ... ADD COLUMN` para migrar sem perder os dados já gravados. É a forma adotada neste projeto de evoluir o schema sem introduzir uma ferramenta de migrations.

## Relatórios

`OneMonitor.Agent.Core.Services.ReportGenerator` agrega uma lista de `ActivityRecord` (sem nenhuma I/O) em um `ReportData`: tempo total ativo (por aplicativo, por janela/site e por categoria), inativo, justificado e não classificado. `OneMonitor.Agent.Infrastructure.Reporting.ReportFileWriter` escreve esse resultado em JSON (`System.Text.Json`) e HTML (template simples) dentro de `data/reports` (ou `%LOCALAPPDATA%\OneMonitor\reports`).

## UI (bandeja do sistema)

`TrayApplicationContext` (WinForms `ApplicationContext` + `NotifyIcon`) expõe um menu de contexto com: status atual (aplicativo + duração), pausar/retomar, gerar relatório do dia, encerrar. Não há janela principal — é intencionalmente uma aplicação "invisível" além do ícone, exceto pelo pop-up de justificativa (ver abaixo).

## Categorias (Fase 2)

`ICategoryResolver.Resolve(processName, windowTitle)` decide a categoria de um instante de atividade. A implementação padrão, `RuleBasedCategoryResolver` (Core, sem I/O, testável), recebe uma lista ordenada de `CategoryDefinition` — cada uma com um nome e uma lista de `CategoryRule` (processo + opcionalmente um trecho do título da janela) — e retorna a categoria da **primeira** que bater; sem match, cai na categoria padrão (`"Outros"`). A ordem importa: regras mais específicas (ex.: `chrome` + título contendo "GitHub" → "Desenvolvimento") precisam estar em categorias declaradas **antes** das genéricas (ex.: `chrome` → "Navegação").

`CategoryRulesLoader` (Infrastructure) só faz a leitura/parse de `categories.json` e monta o resolver — nenhuma lógica de match vive fora do Core. O arquivo fica em `OneMonitor.Agent/categories.json`, copiado para o output como o `appsettings.json`, e seu caminho pode ser sobrescrito via `appsettings.json` (`CategoriesConfigPath`).

## Justificativa de ausência (Fase 2)

`ActivityTracker` expõe o evento `IdleReturned`, disparado em `Tick()` quando a atividade sai de `Idle` para qualquer outro tipo (nunca a partir de `Flush()` — pausar/encerrar não deve interromper o usuário). O evento carrega o `Id` do registro de inatividade já persistido (por isso `IActivityRepository.SaveClosedRecord` retorna esse `Id`) e a duração do período.

`TrayApplicationContext` assina esse evento: pausa o timer de polling, exibe `JustificationPromptForm` (WinForms simples, motivos pré-definidos como botões + opção de pular) e, se o usuário escolher um motivo, chama `ActivityTracker.SetJustification(id, motivo)`, que grava via `IActivityRepository.SetJustification`. O motivo fica associado ao registro de inatividade (`ActivityRecord.Justification`, nullable) e aparece no relatório separado do tempo inativo genérico.

## Sincronização (Fase 3)

Esta fase prepara o agente para enviar dados a um backend. A sincronização vem **desabilitada por padrão** (`Sync.Enabled: false` em `appsettings.json`) — o agente continua 100% funcional offline sem ela; quem liga é a configuração local de cada instalação (ver nota sobre `appsettings.Local.json` logo abaixo), nunca o `appsettings.json` versionado.

### Fila offline

`ActivityRecords` ganha duas colunas (migradas automaticamente, mesmo mecanismo de `Justification`):

- `EventId` — GUID gerado na criação do registro (`ActivityTracker.CreateRecord`), usado pelo backend como chave de deduplicação/upsert. Bancos de fases anteriores sem essa coluna recebem um backfill (`Guid.NewGuid()` por linha) na primeira abertura após a migração.
- `SyncedAt` — `NULL` = pendente de envio; preenchido com o instante da confirmação de sucesso. Quando o `SaveClosedRecord` funde um registro novo com um já sincronizado (mesma atividade contígua — ver "Agrupamento na persistência"), `SyncedAt` é resetado para `NULL`: a parte estendida do bloco precisa ser reenviada, com o mesmo `EventId` (o backend trata isso como upsert, não duplicata).

`IActivityRepository.GetPendingSyncRecords(maxCount)` lê a fila; `MarkSynced(ids, syncedAt)` a esvazia após confirmação.

### `SyncService` (Core, sem I/O)

A cada chamada de `SyncPendingAsync()`: busca até `BatchSize` registros pendentes, monta um `SyncBatch` (`DeviceId` + lista de `ActivitySyncEvent`) e chama `ISyncApiClient.SendBatchAsync`. Em caso de...

- **sucesso** → marca os registros do lote como sincronizados;
- **falha transitória** (rede, timeout, 5xx) → retenta com backoff exponencial (1s, 2s, 4s, ...) até `MaxRetries`; se esgotar, deixa os registros pendentes — o próximo ciclo periódico tenta de novo (a fila em si é o mecanismo de retentativa de longo prazo, sem limite);
- **falha permanente** (4xx, ex. API key inválida) → não insiste na mesma chamada, mas também deixa pendente (evita perder dados por um erro de configuração que pode ser corrigido depois).

Quando há mais pendências do que cabem em um lote, cada ciclo processa só um lote — isso evita que um backlog grande trave uma única chamada; o backlog é drenado ao longo de vários ciclos.

`ISyncApiClient` é a única dependência externa do `SyncService` (junto de `IActivityRepository`, `IClock` e `IAsyncDelay`, esta última abstraindo a espera do backoff para os testes não precisarem esperar de verdade) — toda a lógica de lote/retentativa é testável sem rede nem SQLite reais.

### Contratos da API (`Core/Models/SyncModels.cs`)

```csharp
ActivitySyncEvent(EventId, DeviceId, ProcessName, WindowTitle, Type, StartTime, EndTime, Justification, Category)
SyncBatch(DeviceId, Events)
```

`Type` vai como texto (`"Active"`/`"Idle"`/`"Unclassified"`), não o enum numérico — mantém o contrato estável independente de como o enum evoluir no agente. `Category` (adicionado na Fase 4) é resolvida pelo `SyncService` via o mesmo `ICategoryResolver` usado nos relatórios locais (Fase 2) — o backend nunca precisa reimplementar a lógica de categorização.

### Implementação HTTP (Infrastructure) e identidade do dispositivo

`HttpSyncApiClient` (Infrastructure) implementa `ISyncApiClient` com `HttpClient`: 2xx → `Success`; 4xx → `PermanentFailure`; 5xx/408/429/erro de rede/timeout → `TransientFailure`. `DeviceIdentityProvider` gera um `DeviceId` (GUID) na primeira execução e o persiste em `device-id.txt` ao lado do banco — estável entre reinícios. `DeviceId` e `ApiKey` (de `appsettings.json`) vão como headers HTTP (`X-Device-Id`, `X-Api-Key`) em cada requisição.

### Integração no agente

`TrayApplicationContext` dirige o ciclo de sync com um `System.Threading.Timer` próprio (thread de background, não o `System.Windows.Forms.Timer` do polling) — chamadas de rede não devem rodar na thread de UI. Um `Interlocked` simples evita sobrepor ciclos se uma chamada demorar mais que o intervalo configurado. O item de menu "Sincronizar agora" dispara um ciclo manual e mostra o resultado; fica desabilitado quando `Sync.Enabled` é `false`.

### Segredos locais (`appsettings.Local.json`)

`appsettings.json` é versionado e este repositório é público — a `Sync.ApiKey` real nunca deve morar nele. `AppSettings.Load()` lê o `appsettings.json` base e, se existir, mescla por cima um `appsettings.Local.json` (gitignorado, um por instalação) — recursivamente para objetos aninhados como `Sync`, então o arquivo local só precisa conter os campos que está sobrepondo. O parser aceita `// comentários` (`JsonCommentHandling.Skip`), para `appsettings.Local.json.example` poder ser copiado direto sem precisar editar nada além dos valores. Mesmo padrão do `.env.local` no dashboard.

## Dashboard Web e Ingestão (Fase 4)

Detalhes de configuração e scripts ficam em [`src/web-dashboard/README.md`](../src/web-dashboard/README.md); aqui só o desenho.

### Schema no Supabase (`docs/supabase_schema.sql`)

Duas tabelas: `devices` (um registro por `DeviceId` do agente) e `usage_events` (um registro por `ActivitySyncEvent` recebido). `event_id` é `unique` — upsert por esse campo é o mecanismo de deduplicação do lado do servidor, espelhando o `EventId` que o agente já usa para merges locais. `duration_seconds` é uma coluna **gerada** (`ended_at - started_at`), nunca confiada ao cliente. RLS está ativado nas duas tabelas **sem nenhuma policy permissiva** — só a `service_role key` (usada exclusivamente no servidor Next.js) consegue acessar; isso bloqueia leitura/escrita anônima por padrão até a Fase 5 definir policies por usuário, em vez de liberar tudo e restringir depois.

### Ingestão (`src/web-dashboard/app/api/sync/route.ts`)

Recebe o `SyncBatch` do agente, valida o header `X-Api-Key` contra uma env var do servidor, e faz upsert em `devices`/`usage_events` via `@supabase/supabase-js` com a service-role key. Os códigos de status devolvidos espelham deliberadamente a classificação que `HttpSyncApiClient` já faz do lado do agente (2xx sucesso, 4xx permanente, 5xx transitório) — nenhuma mudança foi necessária no agente para a Fase 4 funcionar.

### Dashboard (`app/page.tsx`)

Visão única (sem seleção de usuário/dispositivo — Fase 5), últimas 24h, agregando `usage_events` por aplicativo, por categoria, e listando ausências justificadas. Next.js 16 introduziu "Cache Components" (toda rota é estática por padrão); a busca no Supabase fica isolada num Server Component dentro de um `<Suspense>`, com `await connection()` sinalizando explicitamente que depende de dados por requisição — isso permite `next build` gerar a casca estática da página sem exigir credenciais reais do Supabase no momento do build.

Os gráficos (`components/AppUsageChart.tsx`, `CategoryBreakdownChart.tsx`) seguem o skill de dataviz do projeto: comparação de magnitude vira barra com hue sequencial único; parte-todo com poucas categorias vira uma única barra empilhada horizontal com cores categóricas em ordem fixa (paleta validada contra os seis checks de acessibilidade do skill — contraste, separação para daltonismo, etc.) — nunca pizza.

### Contrato TypeScript ↔ C#

`src/web-dashboard/lib/sync/contracts.ts` e `lib/supabase/types.ts` espelham manualmente `ActivitySyncEvent`/`SyncBatch` do C#. Não há geração automática entre os dois ecossistemas — qualquer mudança de contrato precisa ser replicada nos dois lados (e os testes de cada lado atualizados).

## Extensibilidade para fases futuras

- `ReportData` é um modelo plano, serializável, que pode ser reaproveitado por uma camada de insights de IA (Fase 6) sem reprocessar os dados brutos.
- A IA nunca entra no caminho de `ActivityTracker`/`IdleTimeProvider`/`ActiveWindowProvider` — qualquer uso futuro de IA consome `ReportData`/`usage_events` já consolidados, nunca o loop de monitoramento em tempo real.
- A Fase 5 (login/multiempresa) adiciona policies de RLS por usuário no Supabase e um seletor de dispositivo/período no dashboard — nenhuma mudança de schema além de novas policies e, possivelmente, uma tabela de usuários/organizações.
