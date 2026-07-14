import { makeAutoObservable, runInAction } from "mobx";
import { api } from "@/lib/api";
import type {
  AppNotification,
  IngestionRunSummary,
  NotificationFeed,
} from "@/lib/types";

/**
 * Synchronisation history + the operator notification feed.
 *
 * It also owns `version` — the counter every page watches to know that an upload has landed and
 * their data is now stale. This is how "refresh the affected pages without a manual reload" is done
 * without a websocket: the app already has no push transport, so rather than bolt one on, the one
 * moment we *know* the data changed (an upload returning 200 in this tab) is broadcast through MobX,
 * and each page re-fetches through the loader it already has.
 *
 * The consequence, stated honestly: this only refreshes the tab that performed the upload. A second
 * operator's tab still waits for its 30s poll. That is the pre-existing polling model, unchanged —
 * making it instant for everyone needs a real push channel, which is a bigger decision than this
 * feature should make on its own.
 */
export class SyncStore {
  /** Bumped every time an upload lands. Pages react to it and re-fetch. */
  version = 0;

  runs: IngestionRunSummary[] = [];
  total = 0;
  loading = false;
  error: string | null = null;

  search = "";
  selectedRunId: string | null = null;

  notifications: AppNotification[] = [];
  unreadCount = 0;

  /** The report from the most recent upload in this session, shown by the upload modal. */
  lastReport: IngestionRunSummary | null = null;

  private _notificationsTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    makeAutoObservable(
      this,
      // The timer handle must not be observable: an autorun that reads it while the interval
      // reassigns it would never converge. Same reasoning as OptimizeStore.
      { _notificationsTimer: false } as never,
      { autoBind: true },
    );
  }

  boot(): void {
    /* no persisted state — the history is server-side by definition */
  }

  get selectedRun(): IngestionRunSummary | null {
    if (!this.selectedRunId) return null;
    return this.runs.find((r) => r.ingestionRunId === this.selectedRunId) ?? null;
  }

  setSearch(value: string): void {
    this.search = value;
  }

  select(runId: string | null): void {
    this.selectedRunId = runId;
  }

  async loadRuns(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const page = await api.network.ingestionRuns({
        search: this.search || undefined,
        take: 50,
      });
      runInAction(() => {
        this.runs = page.runs;
        this.total = page.total;
        this.loading = false;
      });
    } catch (e) {
      runInAction(() => {
        this.error = e instanceof Error ? e.message : String(e);
        this.loading = false;
      });
    }
  }

  /**
   * Called by the upload flow once the pipeline has returned. Records the report, refreshes the
   * history and the notification feed, and bumps `version` so every other page reloads itself.
   */
  recordUpload(report: IngestionRunSummary): void {
    this.lastReport = report;
    this.version += 1;

    void this.loadRuns();
    void this.loadNotifications();
  }

  async loadNotifications(): Promise<void> {
    try {
      const feed: NotificationFeed = await api.notifications.list({ take: 30 });
      runInAction(() => {
        this.notifications = feed.items;
        this.unreadCount = feed.unreadCount;
      });
    } catch {
      // A failing notification poll must never take the page down with it — the bell just
      // keeps showing what it last knew.
    }
  }

  startNotificationsRefresh(): void {
    void this.loadNotifications();
    if (this._notificationsTimer) return;
    this._notificationsTimer = setInterval(() => void this.loadNotifications(), 30_000);
  }

  stopNotificationsRefresh(): void {
    if (this._notificationsTimer) clearInterval(this._notificationsTimer);
    this._notificationsTimer = null;
  }

  async markRead(id: string): Promise<void> {
    // Optimistic: the bell should respond to the click, not to the round-trip.
    const n = this.notifications.find((x) => x.id === id);
    if (n && !n.isRead) {
      runInAction(() => {
        n.isRead = true;
        this.unreadCount = Math.max(0, this.unreadCount - 1);
      });
    }

    try {
      await api.notifications.markRead(id);
    } catch {
      void this.loadNotifications(); // reconcile with the server on failure
    }
  }

  async markAllRead(): Promise<void> {
    runInAction(() => {
      this.notifications.forEach((n) => (n.isRead = true));
      this.unreadCount = 0;
    });

    try {
      await api.notifications.markAllRead();
    } catch {
      void this.loadNotifications();
    }
  }

  dispose(): void {
    this.stopNotificationsRefresh();
  }
}
