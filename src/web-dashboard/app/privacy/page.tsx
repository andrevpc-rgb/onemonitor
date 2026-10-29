import Link from "next/link";

export const metadata = { title: "Privacidade — OneMonitor" };

export default function PrivacyPage() {
  return (
    <main className="mx-auto max-w-2xl space-y-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">Privacidade e dados coletados</h1>
        <p className="mt-1 text-sm" style={{ color: "#898781" }}>
          Última atualização: outubro de 2026.
        </p>
      </header>

      <section className="space-y-3 text-sm" style={{ color: "#0b0b0b" }}>
        <p>
          O OneMonitor é um software de monitoramento de produtividade instalado pela sua
          organização no computador de trabalho. Esta página explica, de forma direta, o que
          é coletado e o que <strong>não</strong> é.
        </p>

        <h2 className="pt-2 text-base font-semibold">O que é coletado</h2>
        <ul className="list-disc space-y-1 pl-5">
          <li>Nome do processo e título da janela em primeiro plano (ex.: &ldquo;chrome.exe&rdquo; / &ldquo;OneMonitor — Dashboard&rdquo;).</li>
          <li>Duração de cada período de atividade e de inatividade (sem movimento de mouse/teclado).</li>
          <li>Categoria associada ao processo/janela (ex.: &ldquo;Navegação&rdquo;), definida pela sua organização.</li>
          <li>Motivo informado voluntariamente num período de ausência, quando o colaborador escolhe justificá-lo.</li>
          <li>Sinais de que o agente está em execução (início, parada, pulsação periódica).</li>
        </ul>

        <h2 className="pt-2 text-base font-semibold">O que NUNCA é coletado</h2>
        <ul className="list-disc space-y-1 pl-5">
          <li>Capturas de tela ou gravação de vídeo.</li>
          <li>Áudio (microfone) ou webcam.</li>
          <li>Teclas digitadas (sem keylogging) — nem conteúdo de documentos, mensagens ou formulários.</li>
          <li>Histórico de navegação detalhado (URLs) — só o título da janela e o nome do processo.</li>
          <li>Qualquer dado quando o monitoramento está pausado ou o agente encerrado.</li>
        </ul>

        <h2 className="pt-2 text-base font-semibold">Quem acessa os dados</h2>
        <p>
          Proprietários e gestores da sua organização, através do dashboard web — sempre
          restritos aos dados da própria organização (nunca de outra empresa que também use
          o OneMonitor). Auditoria de alterações sensíveis (configurações, revogação de
          acesso) fica registrada e visível só para eles.
        </p>

        <h2 className="pt-2 text-base font-semibold">Retenção</h2>
        <p>
          Cada organização define por quantos dias manter os dados de atividade (em
          Configurações) — o padrão é 180 dias, depois disso os eventos são apagados
          automaticamente.
        </p>

        <p className="pt-2 text-xs" style={{ color: "#898781" }}>
          Este texto descreve o funcionamento técnico do software. Não é um documento
          jurídico nem substitui a Política de Privacidade formal da sua organização, que
          deve ser revisada por um profissional de direito antes de ser usada como aviso de
          conformidade (ex.: LGPD).
        </p>
      </section>

      <Link href="/login" className="text-sm font-medium underline">
        Voltar
      </Link>
    </main>
  );
}
