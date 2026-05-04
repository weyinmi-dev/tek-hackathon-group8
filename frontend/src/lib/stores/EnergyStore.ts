import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type { DieselTracePoint, EnergyKpiDto, EnergySiteDto } from "@/lib/types";
import { hydrate, persist } from "./persistence";

const ENERGY_KEY = "tp_energy_v1";

export type EnergyHealthFilter = "all" | EnergySiteDto["health"];

interface EnergySnapshot {
  filter: EnergyHealthFilter;
  selectedId: string | null;
}

/**
 * Domain store for the Energy Sites page. Persists the health filter and the
 * selected site so navigating away to Optimize / Anomalies / Copilot and back
 * keeps the operator's last view intact. The volatile bits — sites, kpis, the
 * 24h diesel trace — are re-fetched on boot and on a 30s interval (kept here so
 * the timer survives page-component remounts).
 */
export class EnergyStore {
  sites: EnergySiteDto[] = [];
  kpis: EnergyKpiDto[] = [];
  trace: DieselTracePoint[] = [];
  filter: EnergyHealthFilter = "all";
  selectedId: string | null = null;
  loading = false;
  error: string | null = null;
  busy: string | null = null;
  toast: { id: string; msg: string } | null = null;
  hasHydrated = false;

  private _disposePersist: (() => void) | null = null;
  private _refreshTimer: ReturnType<typeof setInterval> | null = null;
  private _toastTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;
    hydrate<EnergySnapshot>(ENERGY_KEY, snap => Object.assign(this, snap));
    this._disposePersist = autorun(() => persist(ENERGY_KEY, this.snapshot));
    runInAction(() => { this.hasHydrated = true; });
  }

  get snapshot(): EnergySnapshot {
    return { filter: this.filter, selectedId: this.selectedId };
  }

  get visible(): EnergySiteDto[] {
    return this.filter === "all" ? this.sites : this.sites.filter(s => s.health === this.filter);
  }

  get selected(): EnergySiteDto | null {
    if (!this.selectedId) return null;
    return this.sites.find(s => s.id === this.selectedId) ?? null;
  }

  get counts() {
    return {
      ok: this.sites.filter(s => s.health === "ok").length,
      degraded: this.sites.filter(s => s.health === "degraded").length,
      critical: this.sites.filter(s => s.health === "critical").length,
    };
  }

  setFilter(f: EnergyHealthFilter): void { this.filter = f; }
  setSelected(id: string | null): void { this.selectedId = id; }

  /** Initial load + start the 30s polling timer (idempotent). */
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
      const [s, k] = await Promise.all([api.energy.sites(), api.energy.kpis()]);
      runInAction(() => {
        this.sites = s.sites;
        this.kpis = k.kpis;
        // Default selection: keep current if still present, else first critical, else first.
        const stillThere = this.selectedId && s.sites.find(x => x.id === this.selectedId);
        if (!stillThere) {
          this.selectedId = (s.sites.find(x => x.health === "critical") ?? s.sites[0])?.id ?? null;
        }
      });
      // Refresh the trace too so the chart matches the current selection.
      if (this.selectedId) await this.loadTrace(this.selectedId);
    } catch (e) {
      runInAction(() => { this.error = e instanceof Error ? e.message : String(e); });
    } finally {
      runInAction(() => { this.loading = false; });
    }
  }

  async loadTrace(siteCode: string, hours = 24): Promise<void> {
    try {
      const r = await api.energy.siteDieselTrace(siteCode, hours);
      runInAction(() => { this.trace = r.points; });
    } catch {
      runInAction(() => { this.trace = []; });
    }
  }

  async switchSource(target: EnergySiteDto["source"]): Promise<void> {
    const sel = this.selected;
    if (!sel) return;
    this.busy = "switch";
    try {
      await api.energy.switchSource(sel.id, target);
      await this.refresh();
      this.flash(sel.id, `Source switched to ${target.toUpperCase()}`);
    } catch (e) {
      this.flash(sel.id, e instanceof Error ? e.message : "Switch failed");
    } finally {
      runInAction(() => { this.busy = null; });
    }
  }

  async dispatchRefuel(litres = 60): Promise<void> {
    const sel = this.selected;
    if (!sel) return;
    this.busy = "refuel";
    try {
      const r = await api.energy.refuel(sel.id, litres);
      await this.refresh();
      this.flash(sel.id, `Refuelled +${r.pctChange}% (now ${r.dieselPctAfter}%)`);
    } catch (e) {
      this.flash(sel.id, e instanceof Error ? e.message : "Refuel failed");
    } finally {
      runInAction(() => { this.busy = null; });
    }
  }

  flash(id: string, msg: string): void {
    this.toast = { id, msg };
    if (this._toastTimer) clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(() => {
      runInAction(() => {
        if (this.toast?.id === id) this.toast = null;
      });
    }, 2400);
  }

  dispose(): void {
    this._disposePersist?.();
    this.stopAutoRefresh();
    if (this._toastTimer) clearTimeout(this._toastTimer);
  }
}
