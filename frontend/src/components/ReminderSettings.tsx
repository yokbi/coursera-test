"use client";

import { useState } from "react";
import { authApi } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { tr } from "@/lib/i18n/tr";
import { Field, FormError, inputClass, primaryButtonClass } from "@/components/ui";

const HOURS = Array.from({ length: 24 }, (_, hour) => hour);

export function ReminderSettings() {
  const { user, setUser } = useAuth();
  const [enabled, setEnabled] = useState(user?.remindersEnabled ?? false);
  const [hour, setHour] = useState(user?.reminderHour ?? 20);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      const updated = await authApi.updateReminders(enabled, hour);
      setUser(updated);
      setMessage(tr.settings.remindersSaved);
    } catch {
      setError(tr.settings.saveError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={save} className="flex flex-col gap-3">
      <FormError message={error} />
      {message ? (
        <p
          role="status"
          className="rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300"
        >
          {message}
        </p>
      ) : null}

      <div className="flex items-start gap-2">
        <input
          id="reminders-enabled"
          type="checkbox"
          checked={enabled}
          onChange={(e) => setEnabled(e.target.checked)}
          className="mt-1 h-4 w-4 accent-sky-600 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500"
        />
        <label htmlFor="reminders-enabled" className="text-sm font-medium">
          {tr.settings.remindersEnabled}
        </label>
      </div>

      <p className="text-xs text-slate-500 dark:text-slate-400">{tr.settings.remindersHelp}</p>

      <Field label={tr.settings.reminderHour} htmlFor="reminder-hour">
        <select
          id="reminder-hour"
          value={hour}
          disabled={!enabled}
          onChange={(e) => setHour(Number(e.target.value))}
          className={inputClass}
        >
          {HOURS.map((value) => (
            <option key={value} value={value}>
              {String(value).padStart(2, "0")}:00
            </option>
          ))}
        </select>
      </Field>

      <div>
        <button type="submit" disabled={busy} className={primaryButtonClass}>
          {busy ? tr.common.loading : tr.habits.save}
        </button>
      </div>
    </form>
  );
}
