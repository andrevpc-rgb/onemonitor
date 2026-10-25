# Privacidade

O OneMonitor é desenhado para ser auditável e transparente sobre o que coleta. Estes princípios são restrições de design, não apenas políticas:

## O que é capturado

- **Nome do processo em primeiro plano** (ex.: `chrome`, `devenv`, `Code`).
- **Título da janela ativa** (texto exibido na barra de título, ex.: "README.md - OneMonitor - Visual Studio Code").
- **Timestamps de início/fim** de cada período de atividade e sua duração.
- **Tempo desde o último input de teclado/mouse** (apenas o valor numérico, via `GetLastInputInfo`), usado só para decidir se o usuário está inativo.

## O que nunca é capturado

- **Nenhuma tecla ou clique individual.** O agente não instala hooks globais de teclado/mouse (`WH_KEYBOARD_LL`/`WH_MOUSE_LL`). `GetLastInputInfo` devolve apenas um timestamp do sistema — não há acesso ao conteúdo do input.
- **Nenhuma captura de tela**, nem periódica nem sob demanda.
- **Nenhuma leitura de conteúdo de tela** (OCR, acessibilidade, ou qualquer outra forma).
- **Nenhuma senha, mensagem ou conteúdo de formulário.**
- **Nenhuma chamada de IA** no caminho de monitoramento em tempo real. A Fase 1 não tem nenhuma dependência de API de IA.
- **Nenhum dado sai da máquina.** Tudo é armazenado localmente em SQLite; não há sincronização de rede nesta fase.

## Limitação conhecida

O **título da janela** pode, em alguns aplicativos, conter informação sensível (ex.: nome de um documento confidencial, assunto de um e-mail). Isso é inerente a qualquer monitoramento de "janela ativa" e é diferente de capturar o *conteúdo* da janela. Fica registrado aqui como limitação conhecida da Fase 1; uma fase futura pode introduzir filtros/redação configuráveis de títulos por padrão (ex.: ocultar título de apps de navegador privado) antes de qualquer sincronização com a nuvem — ver [roadmap.md](roadmap.md).

## Controle do usuário

O usuário pode, a qualquer momento, pausar ou encerrar o monitoramento pelo menu do ícone na bandeja do sistema (ver [architecture.md](architecture.md)).
