import { configureApi } from "@/lib/api";
import { AuthStore } from "./AuthStore";
import { ChatStore } from "./ChatStore";
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
 */
export class RootStore {
  readonly auth: AuthStore;
  readonly chat: ChatStore;
  readonly ui: UiStore;

  constructor() {
    this.auth = new AuthStore();
    this.ui = new UiStore();
    this.chat = new ChatStore(this.auth);
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
  }
}
