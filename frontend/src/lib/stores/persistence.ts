/**
 * Tiny helper for persisting MobX store fragments to localStorage. We hand-roll
 * persistence (vs. mobx-persist-store) because:
 *   - We want explicit control over WHAT we persist per store (auth tokens yes,
 *     full chat message bodies no — those can be large and live server-side).
 *   - Cross-tab sync via the `storage` event is trivial to add here.
 *   - Zero extra dependency footprint on top of mobx + mobx-react-lite.
 *
 * Usage from a store:
 *   constructor() {
 *     makeAutoObservable(this);
 *     hydrate(KEY, snapshot => Object.assign(this, snapshot));
 *     autorun(() => persist(KEY, this.snapshot));
 *   }
 */

export function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export function persist<T>(key: string, value: T): void {
  if (!isBrowser()) return;
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Quota exceeded / private mode — non-fatal, store keeps running in memory.
  }
}

export function hydrate<T>(key: string, apply: (snapshot: T) => void): void {
  if (!isBrowser()) return;
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return;
    apply(JSON.parse(raw) as T);
  } catch {
    // Corrupted blob — reset rather than crash.
    window.localStorage.removeItem(key);
  }
}

export function clearPersisted(key: string): void {
  if (!isBrowser()) return;
  window.localStorage.removeItem(key);
}

/**
 * Subscribes to cross-tab changes via the `storage` event. Returns a disposer.
 * Browser only.
 */
export function onCrossTabChange<T>(key: string, handler: (next: T | null) => void): () => void {
  if (!isBrowser()) return () => {};
  const listener = (e: StorageEvent) => {
    if (e.key !== key) return;
    if (e.newValue === null) { handler(null); return; }
    try { handler(JSON.parse(e.newValue) as T); } catch { /* ignore corrupt cross-tab payload */ }
  };
  window.addEventListener("storage", listener);
  return () => window.removeEventListener("storage", listener);
}
