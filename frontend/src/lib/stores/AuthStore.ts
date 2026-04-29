import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api, ApiError } from "@/lib/api";
import type { AuthUser, LoginResponse } from "@/lib/types";
import { hydrate, onCrossTabChange, persist, clearPersisted } from "./persistence";

const AUTH_KEY = "tp_auth_v1";
// Cookie kept for the (server-side) home redirect in app/page.tsx — non-HttpOnly,
// matches the previous behavior. The store is the source of truth at runtime.
const COOKIE = "tp_access";
// Refresh access token N seconds before it expires so a request never lands on
// a freshly-expired token.
const REFRESH_LEAD_SECONDS = 60;

interface AuthSnapshot {
  user: AuthUser | null;
  accessToken: string | null;
  refreshToken: string | null;
  accessExpiresAtUtc: string | null;
  refreshExpiresAtUtc: string | null;
}

const EMPTY: AuthSnapshot = {
  user: null,
  accessToken: null,
  refreshToken: null,
  accessExpiresAtUtc: null,
  refreshExpiresAtUtc: null,
};

/**
 * AuthStore — single source of truth for the signed-in user, JWT pair, and
 * proactive refresh. Hydrates from localStorage on construction so a refresh
 * does not bounce through /login. Cross-tab sync via the storage event keeps
 * two tabs aligned (logging out in one logs out the other).
 */
export class AuthStore {
  user: AuthUser | null = null;
  accessToken: string | null = null;
  refreshToken: string | null = null;
  accessExpiresAtUtc: string | null = null;
  refreshExpiresAtUtc: string | null = null;
  hasHydrated = false;

  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private _disposeCrossTab: (() => void) | null = null;
  private _disposePersist: (() => void) | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /**
   * Browser-only hydration. Called from <StoreProvider> in a useEffect so server-rendered
   * and client first-paint markup match (hasHydrated=false on both). Without this, reading
   * localStorage during construction would diverge SSR from CSR and trigger a hydration
   * mismatch — which tears down the store and loses any in-flight requests' Bearer header.
   */
  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;

    hydrate<AuthSnapshot>(AUTH_KEY, snap => {
      Object.assign(this, snap);
    });

    // Persist on every relevant mutation. Reading the snapshot inside the autorun
    // makes it tracked → writes only fire when something actually changes.
    this._disposePersist = autorun(() => persist(AUTH_KEY, this.snapshot));

    // Sync sessions across tabs: another tab logs in/out → we mirror the new state.
    this._disposeCrossTab = onCrossTabChange<AuthSnapshot>(AUTH_KEY, next => {
      runInAction(() => {
        Object.assign(this, next ?? EMPTY);
        this.scheduleRefresh();
      });
    });

    this.scheduleRefresh();
    runInAction(() => { this.hasHydrated = true; });
  }

  get snapshot(): AuthSnapshot {
    return {
      user: this.user,
      accessToken: this.accessToken,
      refreshToken: this.refreshToken,
      accessExpiresAtUtc: this.accessExpiresAtUtc,
      refreshExpiresAtUtc: this.refreshExpiresAtUtc,
    };
  }

  get isAuthenticated(): boolean {
    return !!this.accessToken && !!this.user;
  }

  /** Logs in via the API and pins the resulting session. */
  async login(email: string, password: string): Promise<void> {
    const r = await api.login(email, password);
    runInAction(() => this.applyLogin(r));
  }

  /** Clears all session state, the cookie, and any pending refresh. */
  logout(): void {
    runInAction(() => {
      this.user = null;
      this.accessToken = null;
      this.refreshToken = null;
      this.accessExpiresAtUtc = null;
      this.refreshExpiresAtUtc = null;
    });
    clearPersisted(AUTH_KEY);
    if (typeof document !== "undefined") {
      document.cookie = `${COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
    }
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = null;
  }

  /** Manually fire a refresh; called both proactively and on a 401. */
  async refresh(): Promise<boolean> {
    if (!this.refreshToken) return false;
    try {
      const r = await api.refresh(this.refreshToken);
      runInAction(() => this.applyLogin(r));
      return true;
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) {
        this.logout();
      }
      return false;
    }
  }

  /** Disposer — only matters in tests / hot reload; in practice the store outlives the page. */
  dispose(): void {
    this._disposeCrossTab?.();
    this._disposePersist?.();
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
  }

  private applyLogin(r: LoginResponse): void {
    this.user = r.user;
    this.accessToken = r.accessToken;
    this.refreshToken = r.refreshToken;
    this.accessExpiresAtUtc = r.accessExpiresAtUtc;
    this.refreshExpiresAtUtc = r.refreshExpiresAtUtc;
    if (typeof document !== "undefined") {
      document.cookie = `${COOKIE}=${r.accessToken}; Path=/; SameSite=Lax`;
    }
    this.scheduleRefresh();
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
    if (!this.accessExpiresAtUtc || !this.refreshToken) return;

    const expiresAt = new Date(this.accessExpiresAtUtc).getTime();
    const now = Date.now();
    const fireIn = Math.max(5_000, expiresAt - now - REFRESH_LEAD_SECONDS * 1000);

    this.refreshTimer = setTimeout(() => {
      // Fire-and-forget; refresh() handles failure by logging out.
      void this.refresh();
    }, fireIn);
  }
}
