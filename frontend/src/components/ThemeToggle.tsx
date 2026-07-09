"use client";

import { useState } from "react";
import { tr } from "@/lib/i18n/tr";

export function ThemeToggle() {
  // Lazy init reads the class set by the pre-paint theme script; SSR falls back to light.
  const [isDark, setIsDark] = useState(
    () => typeof document !== "undefined" && document.documentElement.classList.contains("dark"),
  );

  function toggle() {
    const next = !isDark;
    setIsDark(next);
    document.documentElement.classList.toggle("dark", next);
    try {
      localStorage.setItem("theme", next ? "dark" : "light");
    } catch {
      // Storage may be unavailable (private mode); the toggle still works for this page.
    }
  }

  return (
    <button
      type="button"
      onClick={toggle}
      suppressHydrationWarning
      aria-label={`${tr.settings.theme}: ${isDark ? tr.settings.themeDark : tr.settings.themeLight}`}
      className="rounded-lg border border-slate-300 dark:border-slate-700 p-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500"
    >
      {isDark ? "🌙" : "☀️"}
    </button>
  );
}
