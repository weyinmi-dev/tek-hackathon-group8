import { configureApi } from "@/lib/api";
import { AuthStore } from "./AuthStore";
import { ChatStore } from "./ChatStore";
import { UiStore } from "./UiStore";

/**
 * Composes the per-domain stores into a single root and wires the api client to
 * the auth store (so the fetch wrapper can attach the access token + auto-refresh
 * on a 401 without importing the store directly).
 *
 * Lives entirely on the client; constructed once per page session inside the
 * StoreProvider. Stores are then accessible anywhere via useStores().
 */
export class RootStore {
  readonly auth: AuthStore;
  readonly chat: ChatStore;
  readonly ui: UiStore;

  constructor() {
    this.auth = new AuthStore();
    this.ui = new UiStore();
    this.chat = new ChatStore(this.auth);

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
