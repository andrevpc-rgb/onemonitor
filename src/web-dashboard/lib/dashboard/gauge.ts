/**
 * Geometria de um gauge em semicírculo (meia-rosca): track em 0–180°, arco colorido até a
 * fração atual, tracinho radial marcando a meta. SVG puro, sem lib de gráficos (convenção do
 * dataviz deste dashboard). `fraction` e `goalFraction` em 0–1.
 */
export function describeHalfArc(cx: number, cy: number, r: number, fraction: number): string {
  const f = Math.max(0, Math.min(1, fraction));
  if (f <= 0) {
    return "";
  }
  const a = Math.PI * (1 - f);
  const x = cx + r * Math.cos(a);
  const y = cy - r * Math.sin(a);
  return `M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${x.toFixed(2)} ${y.toFixed(2)}`;
}

export function describeGoalTick(cx: number, cy: number, r: number, goalFraction: number): string {
  const a = Math.PI * (1 - Math.max(0, Math.min(1, goalFraction)));
  const r1 = r - 12;
  const r2 = r + 12;
  const x1 = cx + r1 * Math.cos(a);
  const y1 = cy - r1 * Math.sin(a);
  const x2 = cx + r2 * Math.cos(a);
  const y2 = cy - r2 * Math.sin(a);
  return `M ${x1.toFixed(2)} ${y1.toFixed(2)} L ${x2.toFixed(2)} ${y2.toFixed(2)}`;
}
