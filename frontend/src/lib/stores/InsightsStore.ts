import { makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { MetricsResponse } from "@/lib/types";

/**
 * Domain store for the Operations Dashboard (Insights). Holds the last fetched
 * metrics payload in memory so flipping between tabs doesn't trigger a flash of
 * empty cards while the new fetch resolves. We do NOT persist the payload —
 * dashboard data is short-lived (24h rolling) and re-fetching on boot keeps it
 * fresh; the win here is *survival across in-session navigation*, not durability.
 */
export class InsightsStore {
  metrics: MetricsResponse | null = null;
  loading = false;
  error: string | null = null;

  private _refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /** Boot is a no-op for this store — it has no persisted state — but kept for parity. */
  boot(): void { /* no-op */ }

  /** Initial load + 30s refresh, matching the page's "auto-refresh 30s" subtitle. */
  startAutoRefresh(): void {
    void this.refresh();
    if (this._refreshTimer) return;
    this._refreshTimer = setInterval(() => void this.refresh(), 30_000);
  }

  stopAutoRefresh(): void {
    if (this._refreshTimer) clearInterval(this._refreshTimer);
    this._refreshTimer = null;
  }

  async refresh(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const r = await api.metrics();
      runInAction(() => { this.metrics = r; });
    } catch (e) {
      runInAction(() => { this.error = e instanceof Error ? e.message : String(e); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  dispose(): void {
    this.stopAutoRefresh();
  }
}
