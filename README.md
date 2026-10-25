# OneMonitor

Plataforma para monitorar, de forma transparente e respeitosa à privacidade, o uso do computador: tempo ativo, aplicativos utilizados, períodos de inatividade e relatórios de uso. Sem leitura de conteúdo de tela, sem captura de teclas, sem IA no caminho crítico do monitoramento.

Este repositório está na **Fase 1**: um protótipo local para Windows (agente desktop apenas, sem dashboard, sem nuvem, sem IA).

## Pré-requisitos

- **.NET 8 SDK** — necessário para compilar, testar e executar o agente. Não detectado no ambiente em que este projeto foi criado.
  - Instalar via winget: `winget install Microsoft.DotNet.SDK.8`
  - Ou baixar em https://dotnet.microsoft.com/download/dotnet/8.0
- Windows 10/11 (o agente usa APIs nativas do Windows — `user32.dll` — e não roda em outro sistema).

## Estrutura

```
onemonitor/
├── docs/
│   ├── architecture.md   # decisões técnicas e desenho do sistema
│   ├── privacy.md        # o que é e o que não é capturado
│   └── roadmap.md        # fases futuras (não implementadas ainda)
├── src/desktop-agent/
│   ├── OneMonitor.Agent.sln
│   ├── OneMonitor.Agent/                  # app WinForms: ícone na bandeja + loop de monitoramento
│   ├── OneMonitor.Agent.Core/             # domínio puro (sem dependência de Windows), testável
│   ├── OneMonitor.Agent.Infrastructure/   # P/Invoke Win32, SQLite, geração de relatórios
│   └── OneMonitor.Agent.Tests/            # testes xUnit
├── data/            # banco SQLite e relatórios gerados em desenvolvimento (gitignored)
└── scripts/         # atalhos de build/test/run
```

## Build, testes e execução

```bash
cd src/desktop-agent
dotnet restore
dotnet build
dotnet test
dotnet run --project OneMonitor.Agent
```

Ou use os scripts em [scripts/](scripts/):

```bash
./scripts/build.ps1
./scripts/test.ps1
./scripts/run.ps1
```

Ao iniciar, o OneMonitor aparece como um ícone na bandeja do sistema (sem janela principal). O menu do ícone permite ver o status atual, pausar/retomar o monitoramento, gerar o relatório do dia e encerrar.

Por padrão os dados ficam em `data/onemonitor.db` dentro deste repositório durante o desenvolvimento (detectado automaticamente pela presença do `.sln`); fora de um checkout de desenvolvimento, o padrão passa a ser `%LOCALAPPDATA%\OneMonitor`. Ambos os caminhos podem ser sobrescritos em `appsettings.json`.

### Limitação conhecida: pasta sincronizada pelo Google Drive

Este repositório vive em `G:\Meu Drive\...` (unidade sincronizada pelo Google Drive). A geração do `apphost.exe` nativo do projeto `OneMonitor.Agent` usa memory-mapped files, que o Google Drive (modo stream) bloqueia de forma intermitente, causando falhas `MSB4018`/`CreateAppHost` no build. Por isso `OneMonitor.Agent.csproj` tem `UseAppHost=false`: `dotnet build`, `dotnet test`, `dotnet run` e `./scripts/*.ps1` continuam funcionando normalmente, só não existe um `.exe` nativo clicável no `bin/`. Se mover o projeto para um disco local (fora de sincronização), remova a linha `<UseAppHost>false</UseAppHost>` do csproj para voltar a gerar o `.exe` nativo.

## Escopo da Fase 1

Implementado: detecção de aplicativo/processo/janela em primeiro plano, duração de atividades, detecção de inatividade (limite configurável, padrão 5 min), agrupamento de atividades consecutivas, persistência local em SQLite, funcionamento 100% offline, relatório diário local em HTML e JSON, separação entre tempo ativo / inativo / não classificado, testes automatizados.

Fora de escopo nesta fase (ver [docs/roadmap.md](docs/roadmap.md)): IA/insights, login, multiempresa, cobrança, sincronização com nuvem, captura de tela, leitura de conteúdo de tela, execução automática de comandos, bloqueio de sites, integração com calendário, extensão de navegador.

Veja [docs/privacy.md](docs/privacy.md) para os princípios de privacidade que guiam o design.
