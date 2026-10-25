# OneMonitor

Plataforma para monitorar, de forma transparente e respeitosa à privacidade, o uso do computador: tempo ativo, aplicativos utilizados, períodos de inatividade e relatórios de uso. Sem leitura de conteúdo de tela, sem captura de teclas, sem IA no caminho crítico do monitoramento.

Este repositório tem dois componentes: o **agente desktop** (Windows) e o **dashboard web** (Supabase + Next.js). Fases 1–4 concluídas — monitoramento local, categorias/justificativas, fila de sincronização e o backend/dashboard que a recebe. Sem login/multiusuário ainda e sem IA no caminho de monitoramento. Veja [docs/roadmap.md](docs/roadmap.md) para o detalhe de cada fase.

## Estrutura

```
onemonitor/
├── docs/
│   ├── architecture.md       # decisões técnicas e desenho do sistema (agente + dashboard)
│   ├── privacy.md            # o que é e o que não é capturado
│   ├── roadmap.md            # fases, concluídas e futuras
│   └── supabase_schema.sql   # schema Postgres (devices, usage_events, RLS)
├── src/
│   ├── desktop-agent/        # agente Windows (.NET 8 / WinForms) — ver seção abaixo
│   └── web-dashboard/        # dashboard web (Next.js) — ver src/web-dashboard/README.md
├── data/            # banco SQLite e relatórios gerados em desenvolvimento (gitignored)
└── scripts/         # atalhos de build/test/run do agente
```

## Agente desktop (`src/desktop-agent/`)

### Pré-requisitos

- **.NET 8 SDK** — `winget install Microsoft.DotNet.SDK.8` ou https://dotnet.microsoft.com/download/dotnet/8.0
- Windows 10/11 (usa APIs nativas do Windows — `user32.dll`).

### Build, testes e execução

```bash
cd src/desktop-agent
dotnet restore
dotnet build
dotnet test
dotnet run --project OneMonitor.Agent
```

Ou use os scripts em [scripts/](scripts/): `./scripts/build.ps1`, `./scripts/test.ps1`, `./scripts/run.ps1`.

Ao iniciar, o OneMonitor aparece como um ícone na bandeja do sistema (sem janela principal). O menu do ícone permite ver o status atual, pausar/retomar o monitoramento, gerar o relatório do dia, sincronizar agora (se habilitado) e encerrar.

Por padrão os dados ficam em `data/onemonitor.db` dentro deste repositório durante o desenvolvimento (detectado automaticamente pela presença do `.sln`); fora de um checkout de desenvolvimento, o padrão passa a ser `%LOCALAPPDATA%\OneMonitor`. Caminhos e a sincronização são configuráveis em `appsettings.json`.

### Sincronização com o backend (opcional, desabilitada por padrão)

Edite `Sync` em `appsettings.json` (`Enabled`, `ApiBaseUrl`, `ApiKey`, `BatchSize`, `SyncIntervalSeconds`, `MaxRetries`) para apontar para o dashboard (`src/web-dashboard`) rodando em algum endereço. Sem isso configurado, o agente funciona exatamente como antes — 100% local.

### Limitação conhecida: pasta sincronizada pelo Google Drive

Este repositório vive em `G:\Meu Drive\...` (unidade sincronizada pelo Google Drive). A geração do `apphost.exe` nativo do projeto `OneMonitor.Agent` usa memory-mapped files, que o Google Drive (modo stream) bloqueia de forma intermitente, causando falhas `MSB4018`/`CreateAppHost` no build. Por isso `OneMonitor.Agent.csproj` tem `UseAppHost=false`: `dotnet build`, `dotnet test`, `dotnet run` e `./scripts/*.ps1` continuam funcionando normalmente, só não existe um `.exe` nativo clicável no `bin/`. Se mover o projeto para um disco local (fora de sincronização), remova a linha `<UseAppHost>false</UseAppHost>` do csproj para voltar a gerar o `.exe` nativo.

O mesmo problema afeta `npm install` em `src/web-dashboard/` — ver a nota equivalente no README daquele diretório.

## Dashboard web (`src/web-dashboard/`)

Next.js (TypeScript + Tailwind + Supabase). Recebe os lotes do agente via `app/api/sync/route.ts` e mostra tempo por aplicativo, por categoria e ausências justificadas. Setup completo, variáveis de ambiente e a limitação do Google Drive para `npm install`: ver [src/web-dashboard/README.md](src/web-dashboard/README.md). Schema do banco: [docs/supabase_schema.sql](docs/supabase_schema.sql).

## Escopo implementado (Fases 1–4)

Agente: detecção de aplicativo/processo/janela em primeiro plano, duração de atividades, detecção de inatividade (limite configurável), agrupamento de atividades consecutivas, persistência local em SQLite, funcionamento 100% offline, relatório diário local em HTML e JSON (ativo/inativo/justificado/não classificado, por categoria, por janela/site, linha do tempo), categorias configuráveis (`categories.json`), pop-up de justificativa de ausência, fila offline de sincronização com retentativa/backoff, testes automatizados (dotnet).

Backend/dashboard: schema Postgres no Supabase com deduplicação e RLS, endpoint de ingestão alinhado ao contrato de retentativa do agente, dashboard de leitura (tempo por app, por categoria, ausências justificadas).

Fora de escopo até agora (ver [docs/roadmap.md](docs/roadmap.md)): login, multiempresa, cobrança, IA/insights, captura de tela, leitura de conteúdo de tela, execução automática de comandos, bloqueio de sites, integração com calendário, extensão de navegador.

Veja [docs/privacy.md](docs/privacy.md) para os princípios de privacidade que guiam o design.
