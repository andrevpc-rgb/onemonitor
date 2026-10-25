export function EmptyState({ message }: { message: string }) {
  return (
    <p className="text-xs" style={{ color: "var(--viz-text-muted)" }}>
      {message}
    </p>
  );
}
