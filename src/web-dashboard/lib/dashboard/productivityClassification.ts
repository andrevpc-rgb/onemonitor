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

export interface CustomProductivityRule {
  keyword: string;
  classification: ProductivityClass;
}

/**
 * Classifica um evento de atividade (app + título da janela) em Produtivo/Neutro/
 * Improdutivo. As regras da organização (ver organization_productivity_rules em
 * docs/supabase_schema.sql, editadas em /productivity-rules) têm prioridade sobre as
 * palavras-chave padrão abaixo — é assim que cada organização ajusta "Claude é produtivo
 * pra mim" sem esperar uma mudança de código. Em ambos os casos, checa improdutivo primeiro
 * (maior risco de inflar a métrica de trabalho silenciosamente), depois produtivo; qualquer
 * coisa fora de toda regra cai em neutro — um app desconhecido não deveria ser acusado de
 * improdutivo nem contar como produtivo de graça.
 */
export function classifyProductivity(
  applicationName: string,
  windowTitle: string,
  customRules: CustomProductivityRule[] = [],
): ProductivityClass {
  const haystack = `${applicationName} ${windowTitle}`.toLowerCase();

  const customMatch = (targetClass: ProductivityClass) =>
    customRules.some((rule) => rule.classification === targetClass && haystack.includes(rule.keyword.toLowerCase()));

  if (customMatch("unproductive")) {
    return "unproductive";
  }
  if (customMatch("productive")) {
    return "productive";
  }
  if (customMatch("neutral")) {
    return "neutral";
  }

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

const BROWSER_PROCESS_NAMES = new Set(["chrome", "msedge", "firefox", "opera", "brave"]);

// Títulos de navegador terminam com " - <Nome do navegador>" (Chrome/Edge/Opera/Brave) ou
// " — <Nome do navegador>" (Firefox usa travessão) — ancorado no fim da string ($), então
// só corta esse sufixo, nunca um hífen que faça parte do próprio título da página.
const BROWSER_TITLE_SUFFIX = /\s[-—]\s(Google Chrome|Mozilla Firefox|Microsoft Edge|Opera|Brave)\s*$/i;

/**
 * Palavra-chave sugerida para a descoberta automática de regras (ver app/api/sync/route.ts).
 * Para navegadores, "chrome"/"firefox" sozinho é vago demais — hospedam qualquer site, de
 * uma planilha fiscal a uma rede social — então extrai o nome do site do título da janela
 * (ex.: "Amazon.com.br" de "Amazon.com.br — Mozilla Firefox"). Para os demais apps, usa o
 * próprio nome do processo, como antes. Retorna null quando não há nada identificável (ex.:
 * aba em branco do navegador, sem site no título).
 */
export function extractDiscoveryKeyword(processName: string, windowTitle: string): string | null {
  if (BROWSER_PROCESS_NAMES.has(processName.toLowerCase())) {
    const match = windowTitle.match(BROWSER_TITLE_SUFFIX);
    if (!match) {
      return null;
    }
    const site = windowTitle.slice(0, match.index).trim();
    return site.length > 0 ? site : null;
  }
  return processName || null;
}

/**
 * Peso do tempo Neutro na fórmula do Gauge de Produtividade: conta só pela metade — nem
 * ignorado (seria punir app desconhecido/neutro como se fosse ócio), nem peso cheio como o
 * Produtivo (seria equiparar "WhatsApp Web" a uma planilha fiscal). Ver TeamProductivityOverview.
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
