import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { EnergyRecommendation, OptimizationProjection } from "@/lib/types";
import { hydrate, persist } from "./persistence";

const OPTIMIZE_KEY = "tp_optimize_v1";

interface OptimizeSnapshot {
  solar: number;
  diesel: number;
  batt: number;
}

/**
 * Domain store for the Cost Optimization page. Persists the three slider values
 * (solar adoption %, diesel ₦/L, battery threshold %) so a manager who tunes a
 * scenario, navigates away to inspect a site, then comes back, finds the same
 * scenario waiting. The projection itself is volatile — recomputed on slider
 * change with a 200ms debounce, same as the page used to do inline.
 */
export class OptimizeStore {
  solar = 44;
  diesel = 900;
  batt = 70;
  projection: OptimizationProjection | null = null;
  recommendations: EnergyRecommendation[] = [];
  loading = false;
  hasHydrated = false;

  private _disposePersist: (() => void) | null = null;
  private _disposeRecompute: (() => void) | null = null;
  private _refreshTimer: ReturnType<typeof setInterval> | null = null;
  private _debounceTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;
    hydrate<OptimizeSnapshot>(OPTIMIZE_KEY, snap => Object.assign(this, snap));
    this._disposePersist = autorun(() => persist(OPTIMIZE_KEY, this.snapshot));
    runInAction(() => { this.hasHydrated = true; });

    // Recompute the projection when any slider changes — debounced so a drag
    // doesn't fire 60 requests. autorun re-runs every observable read.
    this._disposeRecompute = autorun(() => {
      const { solar, diesel, batt } = this;
      if (this._debounceTimer) clearTimeout(this._debounceTimer);
      this._debounceTimer = setTimeout(() => void this.computeProjection(solar, diesel, batt), 200);
    });
  }

  get snapshot(): OptimizeSnapshot {
    return { solar: this.solar, diesel: this.diesel, batt: this.batt };
  }

  setSolar(v: number): void { this.solar = v; }
  setDiesel(v: number): void { this.diesel = v; }
  setBatt(v: number): void { this.batt = v; }

  /** Initial recommendation load + 30s refresh — recommendations track ticker state. */
  startRecommendationsRefresh(): void {
    void this.loadRecommendations();
    if (this._refreshTimer) return;
    this._refreshTimer = setInterval(() => void this.loadRecommendations(), 30_000);
  }

  stopRecommendationsRefresh(): void {
    if (this._refreshTimer) clearInterval(this._refreshTimer);
    this._refreshTimer = null;
  }

  private async computeProjection(solar: number, diesel: number, batt: number): Promise<void> {
    this.loading = true;
    try {
      const r = await api.energy.optimization({
        solarPct: solar,
        dieselPriceNgnPerLitre: diesel,
        batteryThresholdPct: batt,
      });
      runInAction(() => { this.projection = r; });
    } catch {
      // Keep last known projection so the chart doesn't blink during transient errors.
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  private async loadRecommendations(): Promise<void> {
    try {
      const r = await api.energy.recommendations();
      runInAction(() => { this.recommendations = r.recommendations; });
    } catch {
      /* keep last list */
    }
  }

  dispose(): void {
    this._disposePersist?.();
    this._disposeRecompute?.();
    this.stopRecommendationsRefresh();
    if (this._debounceTimer) clearTimeout(this._debounceTimer);
  }
}
