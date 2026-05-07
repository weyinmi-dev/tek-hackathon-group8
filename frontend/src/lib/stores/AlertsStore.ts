import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { Alert } from "@/lib/types";
import { hydrate, persist } from "./persistence";

const ALERTS_KEY = "tp_alerts_v1";

export type AlertSeverityFilter = "all" | "critical" | "warn" | "info";

interface AlertsSnapshot {
  filter: AlertSeverityFilter;
  selectedId: string | null;
  showAcknowledged: boolean;
}

/**
 * Domain store for the Smart Alerts page. Holds:
 *   - the in-flight list (volatile — re-fetched on demand, not persisted)
 *   - the active severity filter (persisted, survives nav + refresh)
 *   - the currently selected alert id (persisted, so the side panel stays open
 *     across tab switches — same alert reselects on return if still present).
 *
 * Persistence shape is intentionally tiny — selecting "selectedId" not "selected"
 * means we don't replay stale full alert payloads after a server-side change.
 */
type CountsShape = { all: number; critical: number; warn: number; info: number };
const ZERO_COUNTS: CountsShape = { all: 0, critical: 0, warn: 0, info: 0 };

export class AlertsStore {
  alerts: Alert[] = [];
  filter: AlertSeverityFilter = "all";
  selectedId: string | null = null;
  // Mirrors AnomaliesStore.showAcknowledged — when false, acknowledged alerts
  // are filtered out client-side via the `visible` getter so the operator's
  // active-work view stays clean by default. Persisted across nav/refresh.
  showAcknowledged = false;
  loading = false;
  error: string | null = null;
  acking: string | null = null;
  actionToast: { id: string; msg: string } | null = null;
  hasHydrated = false;
  counts: CountsShape = ZERO_COUNTS;

  private _disposePersist: (() => void) | null = null;
  private _toastTimer: ReturnType<typeof setTimeout> | null = null;
  private _loadPromise: Promise<void> | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;
    hydrate<AlertsSnapshot>(ALERTS_KEY, snap => Object.assign(this, snap));
    this._disposePersist = autorun(() => persist(ALERTS_KEY, this.snapshot));
    runInAction(() => { this.hasHydrated = true; });
  }

  get snapshot(): AlertsSnapshot {
    return {
      filter: this.filter,
      selectedId: this.selectedId,
      showAcknowledged: this.showAcknowledged,
    };
  }

  // Client-side ack filter — mirrors AnomaliesStore.visible. The backend
  // already returns acknowledged alerts in the default ListAsync path, so
  // this is purely a presentation concern.
  get visible(): Alert[] {
    return this.showAcknowledged
      ? this.alerts
      : this.alerts.filter(a => a.status !== "acknowledged");
  }

  get selected(): Alert | null {
    if (!this.selectedId) return null;
    return this.alerts.find(a => a.id === this.selectedId) ?? null;
  }

  setFilter(f: AlertSeverityFilter): void {
    this.filter = f;
  }

  setSelected(id: string | null): void {
    this.selectedId = id;
  }

  toggleShowAcknowledged(): void {
    this.showAcknowledged = !this.showAcknowledged;
  }

  async load(): Promise<void> {
    if (this._loadPromise) return this._loadPromise;
    this._loadPromise = this._doLoad().finally(() => {
      this._loadPromise = null;
    });
    return this._loadPromise;
  }

  private async _doLoad(): Promise<void> {
    runInAction(() => { this.loading = true; this.error = null; });
    try {
      const r = await api.alerts({ severity: this.filter === "all" ? undefined : this.filter });
      runInAction(() => {
        this.alerts = r;
        const stillThere = this.selectedId && r.find(a => a.id === this.selectedId);
        if (!stillThere) this.selectedId = this.visible[0]?.id ?? null;
      });
      void this.loadCounts();
    } catch (e) {
      console.warn("[AlertsStore] load failed:", e);
      runInAction(() => { this.error = e instanceof Error ? e.message : String(e); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  // Lightweight: hits /alerts/counts (DB-side GROUP BY, no geo enrichment).
  // The sidebar uses this on mount instead of load() so first paint isn't
  // blocked on the full alerts payload + OSM lookups.
  async loadCounts(): Promise<void> {
    try {
      const r = await api.alertsCounts();
      runInAction(() => { this.counts = r; });
    } catch (e) {
      console.warn("[AlertsStore] loadCounts failed:", e);
    }
  }

  async ack(id: string): Promise<void> {
    this.acking = id;
    try {
      await api.ackAlert(id);
      await this.load();
      this.flashAction(id, "Alert acknowledged");
    } finally {
      runInAction(() => { this.acking = null; });
    }
  }

  async assign(id: string, team: string): Promise<void> {
    await api.assignAlert(id, team);
    await this.load();
    this.flashAction(id, `Assigned to ${team}`);
  }

  async dispatch(id: string, target: string): Promise<void> {
    await api.dispatchAlert(id, target);
    await this.load();
    this.flashAction(id, `Field dispatch logged: ${target}`);
  }

  flashAction(id: string, msg: string): void {
    this.actionToast = { id, msg };
    if (this._toastTimer) clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(() => {
      runInAction(() => {
        if (this.actionToast?.id === id) this.actionToast = null;
      });
    }, 2400);
  }

  dispose(): void {
    this._disposePersist?.();
    if (this._toastTimer) clearTimeout(this._toastTimer);
  }
}
