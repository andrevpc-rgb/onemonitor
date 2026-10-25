/**
 * Espelho TypeScript do contrato de sincronização do agente desktop C#
 * (src/desktop-agent/OneMonitor.Agent.Core/Models/SyncModels.cs).
 *
 * Mantenha os dois em sincronia manualmente — são dois ecossistemas distintos
 * (C#/.NET e TypeScript/Next.js), não há geração automática de tipos entre eles.
 */

/** "Active" | "Idle" | "Unclassified" — ActivityType do agente, como texto. */
export type ActivityTypeName = "Active" | "Idle" | "Unclassified";

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

export interface SyncBatch {
  deviceId: string;
  events: ActivitySyncEvent[];
}

const VALID_TYPES: ReadonlySet<string> = new Set<ActivityTypeName>([
  "Active",
  "Idle",
  "Unclassified",
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

  if (!Array.isArray(batch.events)) {
    return false;
  }

  return batch.events.every(isValidSyncEvent);
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
