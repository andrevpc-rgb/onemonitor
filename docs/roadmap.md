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

## Fase 3 — Sincronização e dashboard web (futuro, não iniciado)

- API para enviar dados consolidados do agente para a nuvem.
- Dashboard web para o usuário visualizar seus próprios relatórios.
- Autenticação/login.

## Fase 4 — Multiempresa e gestão (futuro, não iniciado)

- Multi-tenancy (empresas, equipes, gestores).
- Papéis e permissões (usuário vs. gestor).
- Cobrança/planos.

## Fase 5 — Insights com IA (futuro, não iniciado)

- IA aplicada **somente sobre dados já consolidados** (nunca em tempo real, nunca controlando mouse/teclado/computador): resumos, comparações, sugestões de produtividade.
- Nenhum uso de "computer use" como mecanismo de produto.

## Fase 6 — Extensões (futuro, não iniciado)

- Extensão de navegador (contexto complementar de abas/sites, com os mesmos princípios de privacidade).
- Integração com calendário.

Cada fase depende da anterior estar estável e será tratada como um projeto separado de implementação — este repositório não deve avançar para elas sem decisão explícita.
