# Arquitetura — Fase 1 (Agente Desktop)

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

## Relatórios

`OneMonitor.Agent.Core.Services.ReportGenerator` agrega uma lista de `ActivityRecord` (sem nenhuma I/O) em um `ReportData`: tempo total ativo (por aplicativo), inativo e não classificado. `OneMonitor.Agent.Infrastructure.Reporting.ReportFileWriter` escreve esse resultado em JSON (`System.Text.Json`) e HTML (template simples) dentro de `data/reports` (ou `%LOCALAPPDATA%\OneMonitor\reports`).

## UI (bandeja do sistema)

`TrayApplicationContext` (WinForms `ApplicationContext` + `NotifyIcon`) expõe um menu de contexto com: status atual (aplicativo + duração), pausar/retomar, gerar relatório do dia, encerrar. Não há janela principal — é intencionalmente uma aplicação "invisível" além do ícone.

## Extensibilidade para fases futuras

- `IActivityRepository` pode futuramente ganhar uma implementação que também envia os dados para uma API (Fase 2), sem alterar `ActivityTracker`.
- `ReportData` é um modelo plano, serializável, que pode ser reaproveitado por um dashboard web ou por uma camada de insights de IA (Fase 4) sem reprocessar os dados brutos.
- A IA nunca entra no caminho de `ActivityTracker`/`IdleTimeProvider`/`ActiveWindowProvider` — qualquer uso futuro de IA consome `ReportData`/registros já consolidados, nunca o loop de monitoramento em tempo real.
