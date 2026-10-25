/**
 * Paleta categórica em ordem fixa (ver docs do skill de dataviz / app/globals.css) —
 * nunca ciclar hues geradas; acima de 8 categorias, as excedentes caem no último slot.
 */
const SERIES_SLOTS = [
  "var(--viz-series-1)",
  "var(--viz-series-2)",
  "var(--viz-series-3)",
  "var(--viz-series-4)",
  "var(--viz-series-5)",
  "var(--viz-series-6)",
  "var(--viz-series-7)",
  "var(--viz-series-8)",
] as const;

/** Atribui cores na ordem recebida (ex.: categorias já ordenadas por duração). */
export function assignCategoryColors(categories: string[]): Map<string, string> {
  const map = new Map<string, string>();
  categories.forEach((name, index) => {
    map.set(name, SERIES_SLOTS[Math.min(index, SERIES_SLOTS.length - 1)]);
  });
  return map;
}
