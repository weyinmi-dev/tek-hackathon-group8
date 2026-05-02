import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { Alert } from "@/lib/types";
import { hydrate, persist } from "./persistence";

const ALERTS_KEY = "tp_alerts_v1";

export type AlertSeverityFilter = "all" | "critical" | "warn" | "info";

interface AlertsSnapshot {
  filter: AlertSeverityFilter;
  selectedId: string | null;
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
export class AlertsStore {
  alerts: Alert[] = [];
  filter: AlertSeverityFilter = "all";
  selectedId: string | null = null;
  loading = false;
  error: string | null = null;
  acking: string | null = null;
  actionToast: { id: string; msg: string } | null = null;
  hasHydrated = false;

  private _disposePersist: (() => void) | null = null;
  private _toastTimer: ReturnType<typeof setTimeout> | null = null;

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
    return { filter: this.filter, selectedId: this.selectedId };
  }

  get selected(): Alert | null {
    if (!this.selectedId) return null;
    return this.alerts.find(a => a.id === this.selectedId) ?? null;
  }

  get counts() {
    return {
      all: this.alerts.length,
      critical: this.alerts.filter(a => a.sev === "critical").length,
      warn: this.alerts.filter(a => a.sev === "warn").length,
      info: this.alerts.filter(a => a.sev === "info").length,
    };
  }

  setFilter(f: AlertSeverityFilter): void {
    this.filter = f;
  }

  setSelected(id: string | null): void {
    this.selectedId = id;
  }

  async load(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const r = await api.alerts({ severity: this.filter === "all" ? undefined : this.filter });
      runInAction(() => {
        this.alerts = r;
        // Keep a valid selection — prefer the previously-selected alert if it
        // still exists in the new list, otherwise default to the first one so
        // the detail panel never goes blank on filter change.
        const stillThere = this.selectedId && r.find(a => a.id === this.selectedId);
        if (!stillThere) this.selectedId = r[0]?.id ?? null;
      });
    } catch (e) {
      // Surface the failure: console for DevTools + store.error for an in-page
      // banner. Without this both an empty fleet and a 500 response render as
      // an identical blank page, which is undiagnosable.
      console.warn("[AlertsStore] load failed:", e);
      runInAction(() => { this.error = e instanceof Error ? e.message : String(e); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  async ack(id: string): Promise<void> {
    this.acking = id;
    try {
      await api.ackAlert(id);
      await this.load();
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
