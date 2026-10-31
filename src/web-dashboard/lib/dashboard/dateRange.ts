export type RangeKey = "today" | "yesterday" | "7d";

export const RANGE_OPTIONS: { key: RangeKey; label: string }[] = [
  { key: "today", label: "Hoje" },
  { key: "yesterday", label: "Ontem" },
  { key: "7d", label: "Últimos 7 dias" },
];

export function isRangeKey(value: string | undefined): value is RangeKey {
  return value === "today" || value === "yesterday" || value === "7d";
}

/** Dias de calendário cobertos por cada período — usado para escalar metas "por dia" (ex.: Jornada Líquida Alvo) ao longo do período filtrado. */
export const RANGE_DAY_COUNT: Record<RangeKey, number> = {
  today: 1,
  yesterday: 1,
  "7d": 7,
};

export interface ResolvedDateRange {
  sinceIso: string;
  untilIso: string;
  /** Meia-noite (America/Sao_Paulo) do dia que a linha do tempo diária deve exibir. */
  timelineDayStartIso: string;
}

// America/Sao_Paulo não observa horário de verão desde 2019 — um offset fixo evita trazer
// uma biblioteca de fuso horário só para resolver "hoje"/"ontem" neste dashboard.
const BRT_OFFSET_HOURS = 3;

export function brtCalendarDate(instant: Date): { year: number; month: number; day: number } {
  const shifted = new Date(instant.getTime() - BRT_OFFSET_HOURS * 60 * 60 * 1000);
  return { year: shifted.getUTCFullYear(), month: shifted.getUTCMonth(), day: shifted.getUTCDate() };
}

/** Meia-noite em America/Sao_Paulo (03:00 UTC do mesmo dia civil), como instante UTC. */
export function brtMidnightUtc(year: number, month: number, day: number): Date {
  return new Date(Date.UTC(year, month, day, BRT_OFFSET_HOURS, 0, 0));
}

export function resolveDateRange(range: RangeKey, now: Date = new Date()): ResolvedDateRange {
  const today = brtCalendarDate(now);
  const todayStart = brtMidnightUtc(today.year, today.month, today.day);

  switch (range) {
    case "today":
      return {
        sinceIso: todayStart.toISOString(),
        untilIso: now.toISOString(),
        timelineDayStartIso: todayStart.toISOString(),
      };
    case "yesterday": {
      const yesterdayStart = new Date(todayStart.getTime() - 24 * 60 * 60 * 1000);
      return {
        sinceIso: yesterdayStart.toISOString(),
        untilIso: todayStart.toISOString(),
        timelineDayStartIso: yesterdayStart.toISOString(),
      };
    }
    case "7d": {
      const sinceStart = new Date(todayStart.getTime() - 6 * 24 * 60 * 60 * 1000);
      return {
        sinceIso: sinceStart.toISOString(),
        untilIso: now.toISOString(),
        // Gantt de um único dia não combina com uma janela de 7 dias — mostra hoje.
        timelineDayStartIso: todayStart.toISOString(),
      };
    }
  }
}
