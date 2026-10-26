/**
 * Espelho TypeScript do contrato de sincronização do agente desktop C#
 * (src/desktop-agent/OneMonitor.Agent.Core/Models/SyncModels.cs).
 *
 * Mantenha os dois em sincronia manualmente — são dois ecossistemas distintos
 * (C#/.NET e TypeScript/Next.js), não há geração automática de tipos entre eles.
 */

/** "Active" | "Idle" | "Unclassified" — ActivityType do agente, como texto. */
export type ActivityTypeName = "Active" | "Idle" | "Unclassified";

/** "Started" (ao iniciar) | "Heartbeat" (a cada ciclo periódico) | "Stopped" (ao encerrar). */
export type DeviceStatusState = "Started" | "Heartbeat" | "Stopped";

export interface ActivitySyncEvent {
  eventId: string;
  deviceId: string;
  processName: string;
  windowTitle: string;
  type: ActivityTypeName;
  startTime: string; // ISO 8601 (DateTimeOffset.ToString("O") do lado C#)
  endTime: string; // ISO 8601
  justification: string | null;
  category: string;
}

export interface DeviceStatusEvent {
  state: DeviceStatusState;
  timestamp: string; // ISO 8601
}

export interface SyncBatch {
  deviceId: string;
  events: ActivitySyncEvent[];
  /** Opcional — Fase 5. Sinal de vida do agente, independente de haver atividade pendente. */
  status?: DeviceStatusEvent;
}

const VALID_TYPES: ReadonlySet<string> = new Set<ActivityTypeName>([
  "Active",
  "Idle",
  "Unclassified",
]);

const VALID_STATUS_STATES: ReadonlySet<string> = new Set<DeviceStatusState>([
  "Started",
  "Heartbeat",
  "Stopped",
]);

/**
 * Validação mínima de forma (não usa uma lib de schema para manter a dependência zero) —
 * suficiente para rejeitar lotes malformados antes de tocar no banco.
 */
export function isValidSyncBatch(value: unknown): value is SyncBatch {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const batch = value as Record<string, unknown>;
  if (typeof batch.deviceId !== "string" || batch.deviceId.length === 0) {
    return false;
  }

  if (!Array.isArray(batch.events) || !batch.events.every(isValidSyncEvent)) {
    return false;
  }

  return batch.status === undefined || isValidDeviceStatusEvent(batch.status);
}

function isValidSyncEvent(value: unknown): value is ActivitySyncEvent {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const event = value as Record<string, unknown>;
  return (
    typeof event.eventId === "string" &&
    event.eventId.length > 0 &&
    typeof event.deviceId === "string" &&
    typeof event.processName === "string" &&
    typeof event.windowTitle === "string" &&
    typeof event.type === "string" &&
    VALID_TYPES.has(event.type) &&
    typeof event.startTime === "string" &&
    typeof event.endTime === "string" &&
    (event.justification === null || typeof event.justification === "string") &&
    typeof event.category === "string"
  );
}

function isValidDeviceStatusEvent(value: unknown): value is DeviceStatusEvent {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const status = value as Record<string, unknown>;
  return (
    typeof status.state === "string" &&
    VALID_STATUS_STATES.has(status.state) &&
    typeof status.timestamp === "string"
  );
}
