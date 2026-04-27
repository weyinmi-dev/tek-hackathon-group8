"use client";

import { useEffect, useState } from "react";

const STORAGE_KEY = "tp-theme";
type Theme = "dark" | "light";

function applyTheme(t: Theme) {
  document.body.className = `theme-${t}`;
}

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>("dark");
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const stored = (localStorage.getItem(STORAGE_KEY) as Theme | null) ?? "dark";
    setTheme(stored);
    applyTheme(stored);
    setMounted(true);
  }, []);

  const toggle = () => {
    const next: Theme = theme === "dark" ? "light" : "dark";
    setTheme(next);
    localStorage.setItem(STORAGE_KEY, next);
    applyTheme(next);
  };

  // Render the same glyph server-side and pre-mount to keep markup stable.
  const glyph = mounted ? (theme === "dark" ? "☀" : "☾") : "☀";
  const label = mounted ? `Switch to ${theme === "dark" ? "light" : "dark"} mode` : "Toggle theme";

  return (
    <button
      type="button"
      onClick={toggle}
      title={label}
      aria-label={label}
      className="mono"
      style={{
        appearance: "none",
        display: "grid",
        placeItems: "center",
        width: 28,
        height: 28,
        padding: 0,
        background: "var(--bg-1)",
        color: "var(--ink-2)",
        border: "1px solid var(--line)",
        borderRadius: 6,
        cursor: "pointer",
        fontSize: 13,
        lineHeight: 1,
      }}
    >
      {glyph}
    </button>
  );
}
