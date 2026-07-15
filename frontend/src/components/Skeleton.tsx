"use client";

/**
 * Placeholder rows shown while a list is loading for the first time.
 *
 * Only ever shown when there is genuinely nothing to display — a refresh with data already in the
 * store keeps the old rows on screen and swaps them out, which is why the 30s poll doesn't make the
 * page flicker every half minute. This is for the cold load, where a bare "Loading…" line leaves the
 * page looking broken rather than busy.
 */
export function SkeletonRows({
  rows = 5,
  height = 52,
}: {
  rows?: number;
  height?: number;
}) {
  return (
    <div aria-busy="true" aria-live="polite">
      {Array.from({ length: rows }, (_, i) => (
        <div
          key={i}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 12,
            height,
            padding: "0 14px",
            borderBottom: "1px solid var(--line)",
            // Stagger the shimmer down the list so it reads as a sweep rather than a strobe.
            animation: `skeleton-pulse 1.4s ease-in-out ${i * 0.09}s infinite`,
          }}
        >
          <div style={{ width: 8, height: 8, borderRadius: 2, background: "var(--bg-3)" }} />
          <div style={{ flex: 1, display: "grid", gap: 6 }}>
            <div
              style={{
                height: 9,
                width: `${58 - (i % 3) * 9}%`,
                borderRadius: 3,
                background: "var(--bg-3)",
              }}
            />
            <div
              style={{
                height: 7,
                width: `${34 + (i % 4) * 7}%`,
                borderRadius: 3,
                background: "var(--bg-2)",
              }}
            />
          </div>
          <div style={{ width: 46, height: 16, borderRadius: 8, background: "var(--bg-3)" }} />
        </div>
      ))}
    </div>
  );
}
