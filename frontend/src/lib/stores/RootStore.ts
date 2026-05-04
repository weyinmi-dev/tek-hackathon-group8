import { configureApi } from "@/lib/api";
import { AlertsStore } from "./AlertsStore";
import { AnomaliesStore } from "./AnomaliesStore";
import { AuthStore } from "./AuthStore";
import { ChatStore } from "./ChatStore";
import { EnergyStore } from "./EnergyStore";
import { InsightsStore } from "./InsightsStore";
import { OptimizeStore } from "./OptimizeStore";
import { UiStore } from "./UiStore";

/**
 * Composes the per-domain stores into a single root.
 *
 * The api client wiring (configureApi) is intentionally NOT done here — it is
 * done from <StoreProvider>'s useEffect. Reason: with reactStrictMode the
 * useState initializer runs twice in dev, constructing two RootStore instances.
 * useState only keeps the first; if we wired configureApi from the ctor, the
 * module-level token closure would reference the discarded second instance
 * (whose auth.accessToken is always null) → spurious "no bearer" 401s.
 *
 * Domain stores (alerts / anomalies / energy / optimize / insights) follow the
 * same pattern as the foundational ones (auth / chat / ui): construct here,
 * boot from <StoreProvider>'s useEffect, expose narrow hooks for call sites.
 */
export class RootStore {
  readonly auth: AuthStore;
  readonly chat: ChatStore;
  readonly ui: UiStore;

  readonly alerts: AlertsStore;
  readonly anomalies: AnomaliesStore;
  readonly energy: EnergyStore;
  readonly optimize: OptimizeStore;
  readonly insights: InsightsStore;

  constructor() {
    this.auth = new AuthStore();
    this.ui = new UiStore();
    this.chat = new ChatStore(this.auth);

    this.alerts = new AlertsStore();
    this.anomalies = new AnomaliesStore();
    this.energy = new EnergyStore();
    this.optimize = new OptimizeStore();
    this.insights = new InsightsStore();
  }

  wireApi(): void {
    configureApi({
      getAccessToken: () => this.auth.accessToken,
      refresh: () => this.auth.refresh(),
    });
  }

  dispose(): void {
    this.auth.dispose();
    this.chat.dispose();

    this.alerts.dispose();
    this.anomalies.dispose();
    this.energy.dispose();
    this.optimize.dispose();
    this.insights.dispose();
  }
}
