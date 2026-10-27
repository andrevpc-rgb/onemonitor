import type { DeviceRow } from "@/lib/supabase/types";

export type DeviceStatusLabel = "online" | "away" | "offline";

const ONLINE_WINDOW_MINUTES = 3;
const AWAY_WINDOW_MINUTES = 15;

/**
 * "online"/"offline"/"stopped" (devices.status) é só o último sinal autodeclarado pelo
 * agente (ver docs/supabase_schema.sql) — não diz se o dispositivo ainda está vivo agora.
 * Essa derivação, feita na leitura, é a peça que faltava desde a Fase 5: um agente que
 * trava ou perde conexão sem avisar (sem Stopped) precisa aparecer como "offline" depois
 * de um tempo sem heartbeat, não ficar "online" para sempre.
 */
export function deriveDeviceStatus(
  device: Pick<DeviceRow, "status" | "last_seen_at">,
  now: Date = new Date(),
): DeviceStatusLabel {
  if (device.status === "stopped") {
    return "offline";
  }

  const minutesSinceLastSeen = (now.getTime() - new Date(device.last_seen_at).getTime()) / 60_000;

  if (minutesSinceLastSeen <= ONLINE_WINDOW_MINUTES) {
    return "online";
  }
  if (minutesSinceLastSeen <= AWAY_WINDOW_MINUTES) {
    return "away";
  }
  return "offline";
}

export const DEVICE_STATUS_LABELS: Record<DeviceStatusLabel, string> = {
  online: "Online",
  away: "Ausente",
  offline: "Offline",
};
