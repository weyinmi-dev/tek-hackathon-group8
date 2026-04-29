import { autorun, makeAutoObservable, runInAction } from "mobx";
import { hydrate, persist } from "./persistence";

const UI_KEY = "tp_ui_v1";

interface UiSnapshot {
  sidebarCollapsed: boolean;
  chatSidebarCollapsed: boolean;
}

/** UI preferences that should survive a refresh — collapsed sidebars, theme, etc. */
export class UiStore {
  sidebarCollapsed = false;
  chatSidebarCollapsed = false;
  hasHydrated = false;

  private _disposePersist: (() => void) | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /**
   * Browser-only hydration. Same SSR/CSR-match reasoning as AuthStore — reading
   * localStorage during construction would diverge SSR markup from client first-paint
   * (e.g. user previously collapsed the sidebar). Defer until after mount.
   */
  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;
    hydrate<UiSnapshot>(UI_KEY, snap => Object.assign(this, snap));
    this._disposePersist = autorun(() => persist(UI_KEY, this.snapshot));
    runInAction(() => { this.hasHydrated = true; });
  }

  get snapshot(): UiSnapshot {
    return {
      sidebarCollapsed: this.sidebarCollapsed,
      chatSidebarCollapsed: this.chatSidebarCollapsed,
    };
  }

  toggleSidebar(): void { this.sidebarCollapsed = !this.sidebarCollapsed; }
  toggleChatSidebar(): void { this.chatSidebarCollapsed = !this.chatSidebarCollapsed; }

  dispose(): void {
    this._disposePersist?.();
  }
}
