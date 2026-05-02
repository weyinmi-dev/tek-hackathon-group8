"use client";

import { Pill } from "@/components/UI";
import type { GeoSummary } from "@/lib/types";

/**
 * Small inline badge that surfaces the OSM-derived geo context attached to every
 * site / alert / anomaly response. Renders nothing if the backend lookup failed
 * (geo === null) so the layout doesn't shift on missing data.
 *
 * Three readable signals on one row:
 *   • Region type pill (urban / suburban / rural / remote) tinted by accessibility
 *   • Accessibility score (0-100, "How well-served is this site by infrastructure")
 *   • Distance to nearest fuel station (km), the directive's theft-probability proxy
 *
 * The full address (Nominatim display_name) is exposed via title so operators can
 * hover for the postal context without cluttering the card.
 */
export function GeoBadge({ geo, compact = false }: { geo: GeoSummary | null | undefined; compact?: boolean }) {
  if (!geo) return null;

  // Map the four region tiers to existing tones — denser / more accessible regions
  // land on "ok" / "info", increasingly remote ones shift toward "warn".
  const tone = regionTone(geo.regionType);
  const fuelKm = geo.nearestFuelStationMetres != null
    ? (geo.nearestFuelStationMetres / 1000).toFixed(geo.nearestFuelStationMetres < 10_000 ? 1 : 0)
    : null;

  return (
    <div
      title={geo.address ?? `${geo.latitude.toFixed(4)}, ${geo.longitude.toFixed(4)}`}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 6,
        flexWrap: "wrap",
      }}
    >
      <Pill tone={tone} dot>{geo.regionType}</Pill>
      {!compact && (
        <Pill tone="neutral">
          access&nbsp;{Math.round(geo.accessibilityScore)}
        </Pill>
      )}
      {fuelKm != null && (
        <Pill tone={fuelKm && Number(fuelKm) > 5 ? "warn" : "info"}>
          fuel&nbsp;{fuelKm}&nbsp;km
        </Pill>
      )}
    </div>
  );
}

function regionTone(regionType: GeoSummary["regionType"]): "ok" | "info" | "warn" | "crit" {
  switch (regionType) {
    case "urban": return "ok";
    case "suburban": return "info";
    case "rural": return "warn";
    case "remote": return "crit";
  }
}
