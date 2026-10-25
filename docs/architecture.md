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

## Extensibilidade para fases futuras

- `IActivityRepository` pode futuramente ganhar uma implementação que também envia os dados para uma API (Fase 3), sem alterar `ActivityTracker`.
- `ReportData` é um modelo plano, serializável, que pode ser reaproveitado por um dashboard web ou por uma camada de insights de IA (Fase 5) sem reprocessar os dados brutos.
- A IA nunca entra no caminho de `ActivityTracker`/`IdleTimeProvider`/`ActiveWindowProvider` — qualquer uso futuro de IA consome `ReportData`/registros já consolidados, nunca o loop de monitoramento em tempo real.
