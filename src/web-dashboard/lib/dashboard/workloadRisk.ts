import type { DeviceRow, UsageEventRow } from "@/lib/supabase/types";
import { displayDeviceName } from "@/lib/dashboard/deviceDisplay";
import { brtCalendarDate, brtMidnightUtc } from "@/lib/dashboard/dateRange";

export interface WorkloadAlertCriteria {
  enabled: boolean;
  /** Horas ativas/dia a partir das quais um dia conta como "sobrecarregado" (ver /settings). */
  dailyHoursThreshold: number;
  /** Quantos dias consecutivos acima do limiar disparam o alerta. */
  consecutiveDaysThreshold: number;
}

export interface DayWindow {
  /** "31/10" — rótulo curto para exibição. */
  label: string;
  sinceIso: string;
  untilIso: string;
}

export interface WorkloadRiskEntry {
  deviceId: string;
  label: string;
  /** Horas ativas de cada um dos últimos `consecutiveDaysThreshold` dias completos, da mais antiga para a mais recente. */
  dailyHours: number[];
  dayLabels: string[];
}

/**
 * Os últimos `count` dias civis (America/Sao_Paulo) já encerrados, terminando ontem — hoje
 * fica de fora de propósito: um dia ainda em andamento não permite dizer se "não passou do
 * limiar" por ter sido um dia leve ou só por ainda não ter acabado, o que inflaria falsos
 * negativos bem no dia mais recente.
 */
export function lastCompleteBrtDays(count: number, now: Date): DayWindow[] {
  const today = brtCalendarDate(now);
  const todayStart = brtMidnightUtc(today.year, today.month, today.day);

  const days: DayWindow[] = [];
  for (let i = count; i >= 1; i--) {
    const dayStart = new Date(todayStart.getTime() - i * 24 * 60 * 60 * 1000);
    const dayEnd = new Date(dayStart.getTime() + 24 * 60 * 60 * 1000);
    const label = dayStart.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit", timeZone: "UTC" });
    days.push({ label, sinceIso: dayStart.toISOString(), untilIso: dayEnd.toISOString() });
  }
  return days;
}

/**
 * Indicador de risco de sobrecarga (NR-1/GRO, ver docs/supabase_schema.sql) — dispara por
 * dispositivo quando TODOS os últimos `consecutiveDaysThreshold` dias completos têm tempo
 * ativo acima de `dailyHoursThreshold`. Um dia sem nenhuma atividade (dispositivo desligado,
 * fim de semana) conta como abaixo do limiar, então reinicia a sequência — é o comportamento
 * certo aqui: a pergunta é "teve descanso", não "o agente esteve ligado".
 */
export function detectWorkloadRisk(
  events: UsageEventRow[],
  devices: Pick<DeviceRow, "device_id" | "hostname" | "device_name">[],
  criteria: WorkloadAlertCriteria,
  now: Date,
): WorkloadRiskEntry[] {
  if (!criteria.enabled) {
    return [];
  }

  const days = lastCompleteBrtDays(criteria.consecutiveDaysThreshold, now);
  const thresholdSeconds = criteria.dailyHoursThreshold * 3600;

  const activeEventsByDevice = new Map<string, UsageEventRow[]>();
  for (const event of events) {
    if (event.activity_type !== "Active") {
      continue;
    }
    const list = activeEventsByDevice.get(event.device_id);
    if (list) {
      list.push(event);
    } else {
      activeEventsByDevice.set(event.device_id, [event]);
    }
  }

  const results: WorkloadRiskEntry[] = [];

  for (const device of devices) {
    const deviceEvents = activeEventsByDevice.get(device.device_id) ?? [];

    const dailyHours = days.map((day) => {
      let seconds = 0;
      for (const event of deviceEvents) {
        if (event.started_at >= day.sinceIso && event.started_at < day.untilIso) {
          seconds += event.duration_seconds;
        }
      }
      return seconds / 3600;
    });

    const allDaysExceedThreshold = dailyHours.every((hours) => hours * 3600 >= thresholdSeconds);
    if (allDaysExceedThreshold) {
      results.push({
        deviceId: device.device_id,
        label: displayDeviceName(device),
        dailyHours,
        dayLabels: days.map((d) => d.label),
      });
    }
  }

  return results.sort((a, b) => sum(b.dailyHours) - sum(a.dailyHours));
}

function sum(values: number[]): number {
  return values.reduce((total, value) => total + value, 0);
}
