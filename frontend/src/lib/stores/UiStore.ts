import { autorun, makeAutoObservable } from "mobx";
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

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
    hydrate<UiSnapshot>(UI_KEY, snap => Object.assign(this, snap));
    autorun(() => persist(UI_KEY, this.snapshot));
  }

  get snapshot(): UiSnapshot {
    return {
      sidebarCollapsed: this.sidebarCollapsed,
      chatSidebarCollapsed: this.chatSidebarCollapsed,
    };
  }

  toggleSidebar(): void { this.sidebarCollapsed = !this.sidebarCollapsed; }
  toggleChatSidebar(): void { this.chatSidebarCollapsed = !this.chatSidebarCollapsed; }
}
