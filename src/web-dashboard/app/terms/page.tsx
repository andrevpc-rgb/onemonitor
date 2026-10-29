import Link from "next/link";

export const metadata = { title: "Termos de uso — OneMonitor" };

export default function TermsPage() {
  return (
    <main className="mx-auto max-w-2xl space-y-6 p-6">
      <header>
        <h1 className="text-xl font-semibold">Termos de uso</h1>
        <p className="mt-1 text-sm" style={{ color: "#898781" }}>
          Última atualização: outubro de 2026.
        </p>
      </header>

      <section className="space-y-3 text-sm" style={{ color: "#0b0b0b" }}>
        <p>
          Estes termos regem o uso do OneMonitor pela organização que o contratou e pelas
          pessoas que usam o agente desktop ou o dashboard web em nome dela.
        </p>

        <h2 className="pt-2 text-base font-semibold">O que o OneMonitor é</h2>
        <p>
          Uma ferramenta de monitoramento de produtividade: o agente desktop registra
          períodos de atividade/inatividade no computador onde está instalado (ver a{" "}
          <Link href="/privacy" className="underline">
            página de Privacidade
          </Link>{" "}
          para o detalhe exato do que é coletado) e envia esses dados ao dashboard web da
          organização.
        </p>

        <h2 className="pt-2 text-base font-semibold">Responsabilidade da organização</h2>
        <ul className="list-disc space-y-1 pl-5">
          <li>Informar as pessoas monitoradas de que o OneMonitor está instalado e o que ele coleta.</li>
          <li>Cumprir a legislação aplicável ao monitoramento de funcionários na sua jurisdição.</li>
          <li>Controlar quem, dentro da organização, tem acesso de proprietário/gestor ao dashboard.</li>
          <li>Revogar o acesso de dispositivos e remover colaboradores quando apropriado.</li>
        </ul>

        <h2 className="pt-2 text-base font-semibold">Limitações</h2>
        <p>
          O OneMonitor é fornecido &ldquo;como está&rdquo;. Os dados de atividade refletem o que o
          sistema operacional reporta (processo/janela em primeiro plano, tempo sem
          interação) — não há garantia de que isso capture integralmente o trabalho
          realizado, nem o software deve ser usado como única base para decisões
          disciplinares sem outras evidências.
        </p>

        <p className="pt-2 text-xs" style={{ color: "#898781" }}>
          Este texto descreve o funcionamento do produto. Não é um contrato formal nem
          substitui os Termos de Uso definitivos, que devem ser revisados por um
          profissional de direito antes do lançamento comercial.
        </p>
      </section>

      <Link href="/login" className="text-sm font-medium underline">
        Voltar
      </Link>
    </main>
  );
}
