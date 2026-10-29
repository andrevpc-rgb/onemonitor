export type ProductivityClass = "productive" | "neutral" | "unproductive";

/**
 * Palavras-chave vistas no nome do app OU no título da janela — ponto de partida editável,
 * não uma lista exaustiva. Casar no app + título (não só no processo) é o que permite
 * diferenciar "chrome.exe" produtivo (ex.: e-CAC) de improdutivo (ex.: YouTube) sem precisar
 * mudar o agente — ver window_title em docs/supabase_schema.sql (capturado desde a Fase 1).
 */
const UNPRODUCTIVE_KEYWORDS = [
  "youtube",
  "instagram",
  "facebook",
  "tiktok",
  "netflix",
  "twitter",
  "x.com",
  "twitch",
  "spotify",
  "kwai",
];

const PRODUCTIVE_KEYWORDS = [
  "excel",
  "planilha",
  "google sheets",
  "word",
  "powerpoint",
  "outlook",
  "e-cac",
  "ecac",
  "receita federal",
  "gov.br",
  "sped",
  "simples nacional",
  "sefaz",
  "jucesc",
  "e-social",
  "esocial",
  "contábil",
  "contabil",
  "fiscal",
  "erp",
];

const NEUTRAL_KEYWORDS = ["explorer.exe", "file explorer", "gerenciador de arquivos", "whatsapp"];

/**
 * Classifica um evento de atividade (app + título da janela) em Produtivo/Neutro/
 * Improdutivo. Checa improdutivo primeiro (maior risco de inflar a métrica de trabalho
 * silenciosamente), depois produtivo; qualquer coisa fora das listas cai em neutro — um app
 * desconhecido não deveria ser acusado de improdutivo nem contar como produtivo de graça.
 */
export function classifyProductivity(applicationName: string, windowTitle: string): ProductivityClass {
  const haystack = `${applicationName} ${windowTitle}`.toLowerCase();

  if (UNPRODUCTIVE_KEYWORDS.some((keyword) => haystack.includes(keyword))) {
    return "unproductive";
  }
  if (PRODUCTIVE_KEYWORDS.some((keyword) => haystack.includes(keyword))) {
    return "productive";
  }
  if (NEUTRAL_KEYWORDS.some((keyword) => haystack.includes(keyword))) {
    return "neutral";
  }
  return "neutral";
}

/**
 * Peso do tempo Neutro na fórmula do Gauge de Produtividade: conta só pela metade — nem
 * ignorado (seria punir app desconhecido/neutro como se fosse ócio), nem peso cheio como o
 * Produtivo (seria equiparar "WhatsApp Web" a uma planilha fiscal). Ver ProductivityGauge.
 */
export const NEUTRAL_TIME_WEIGHT = 0.5;

export const PRODUCTIVITY_CLASS_LABELS: Record<ProductivityClass, string> = {
  productive: "Produtivo",
  neutral: "Neutro",
  unproductive: "Improdutivo",
};

// Reaproveita os tokens de status fixos do skill de dataviz (nunca usados como série
// categórica) — "neutral" fica em tinta neutra, não é um alerta.
export const PRODUCTIVITY_CLASS_COLOR: Record<ProductivityClass, string> = {
  productive: "var(--viz-status-good)",
  neutral: "var(--viz-text-muted)",
  unproductive: "var(--viz-status-critical)",
};
