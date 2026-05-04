import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { EnergyAnomalyDto } from "@/lib/types";
import { hydrate, persist } from "./persistence";

const ANOMALIES_KEY = "tp_anomalies_v1";

interface AnomaliesSnapshot {
  selectedId: string | null;
  showAcknowledged: boolean;
}

/**
 * Domain store for the Energy Anomalies page. Persists the selection and the
 * show-acknowledged toggle so the operator's filter survives navigation. The
 * raw anomaly list itself is volatile — re-fetched on boot and after acks.
 */
export class AnomaliesStore {
  anomalies: EnergyAnomalyDto[] = [];
  selectedId: string | null = null;
  showAcknowledged = false;
  loading = false;
  error: string | null = null;
  busy: string | null = null;
  toast: string | null = null;
  hasHydrated = false;

  private _disposePersist: (() => void) | null = null;
  private _toastTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;
    hydrate<AnomaliesSnapshot>(ANOMALIES_KEY, snap => Object.assign(this, snap));
    this._disposePersist = autorun(() => persist(ANOMALIES_KEY, this.snapshot));
    runInAction(() => { this.hasHydrated = true; });
  }

  get snapshot(): AnomaliesSnapshot {
    return { selectedId: this.selectedId, showAcknowledged: this.showAcknowledged };
  }

  get visible(): EnergyAnomalyDto[] {
    return this.showAcknowledged
      ? this.anomalies
      : this.anomalies.filter(a => !a.acknowledged);
  }

  get selected(): EnergyAnomalyDto | null {
    if (!this.selectedId) return null;
    return this.anomalies.find(a => a.id === this.selectedId) ?? null;
  }

  setSelected(id: string | null): void {
    this.selectedId = id;
  }

  toggleShowAcknowledged(): void {
    this.showAcknowledged = !this.showAcknowledged;
  }

  async load(take = 50): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const r = await api.energy.anomalies(take);
      runInAction(() => {
        this.anomalies = r.anomalies;
        const stillThere = this.selectedId && r.anomalies.find(a => a.id === this.selectedId);
        if (!stillThere) this.selectedId = this.visible[0]?.id ?? null;
      });
    } catch (e) {
      // Same diagnostic story as AlertsStore: surface in DevTools + store.error
      // so the operator can tell "API failed" apart from "no anomalies".
      console.warn("[AnomaliesStore] load failed:", e);
      runInAction(() => { this.error = e instanceof Error ? e.message : String(e); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  async acknowledge(id: string): Promise<void> {
    this.busy = id;
    try {
      await api.energy.ackAnomaly(id);
      await this.load();
      this.flashToast("Anomaly acknowledged");
    } finally {
      runInAction(() => { this.busy = null; });
    }
  }

  flashToast(msg: string): void {
    this.toast = msg;
    if (this._toastTimer) clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(() => {
      runInAction(() => { this.toast = null; });
    }, 2400);
  }

  dispose(): void {
    this._disposePersist?.();
    if (this._toastTimer) clearTimeout(this._toastTimer);
  }
}
