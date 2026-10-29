import type { DeviceRow } from "@/lib/supabase/types";

/**
 * Nome exibido para um dispositivo: o rótulo editável (device_name) primeiro, depois o que
 * o agente relatou (hostname), e só em último caso o device_id (GUID) — usado em todo
 * lugar que mostra um dispositivo, para nunca cair direto no GUID se houver algo melhor.
 */
export function displayDeviceName(device: Pick<DeviceRow, "device_name" | "hostname" | "device_id">): string {
  return device.device_name || device.hostname || device.device_id;
}
