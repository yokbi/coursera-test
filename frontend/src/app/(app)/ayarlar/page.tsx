"use client";

import { useState } from "react";
import { ApiError, authApi } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { passwordSchema } from "@/lib/habit-schema";
import { ReminderSettings } from "@/components/ReminderSettings";
import { tr } from "@/lib/i18n/tr";
import {
  Field,
  FormError,
  dangerButtonClass,
  inputClass,
  primaryButtonClass,
} from "@/components/ui";

const COMMON_TIMEZONES = [
  "Europe/Istanbul",
  "Europe/Berlin",
  "Europe/London",
  "America/New_York",
  "America/Los_Angeles",
  "Asia/Tokyo",
  "Australia/Sydney",
  "UTC",
];

function TimezoneSection() {
  const { user, setUser } = useAuth();
  const [timeZone, setTimeZone] = useState(user?.timeZone ?? "Europe/Istanbul");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const options = COMMON_TIMEZONES.includes(timeZone)
    ? COMMON_TIMEZONES
    : [timeZone, ...COMMON_TIMEZONES];

  async function save(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      const updated = await authApi.updateTimeZone(timeZone);
      setUser(updated);
      setMessage(tr.settings.timezoneSaved);
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
        <p role="status" className="rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300">
          {message}
        </p>
      ) : null}
      <Field label={tr.settings.timezone} htmlFor="timezone">
        <select
          id="timezone"
          value={timeZone}
          onChange={(e) => setTimeZone(e.target.value)}
          className={inputClass}
        >
          {options.map((zone) => (
            <option key={zone} value={zone}>
              {zone}
            </option>
          ))}
        </select>
        <p className="text-xs text-slate-500 dark:text-slate-400">{tr.settings.timezoneHelp}</p>
      </Field>
      <div>
        <button type="submit" disabled={busy} className={primaryButtonClass}>
          {busy ? tr.common.loading : tr.habits.save}
        </button>
      </div>
    </form>
  );
}

function PasswordSection({ hasPassword }: { hasPassword: boolean }) {
  const { setUser } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setMessage(null);
    setError(null);

    const parsed = passwordSchema.safeParse(newPassword);
    if (!parsed.success) {
      setErrors({ newPassword: parsed.error.issues[0]?.message ?? tr.auth.passwordMin });
      return;
    }
    setErrors({});
    setBusy(true);
    try {
      const result = await authApi.changePassword(currentPassword, newPassword);
      setUser(result.user);
      setMessage(tr.settings.passwordChanged);
      setCurrentPassword("");
      setNewPassword("");
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        setError(tr.settings.currentPasswordWrong);
      } else if (err instanceof ApiError && err.problem?.errors) {
        const first = Object.values(err.problem.errors)[0]?.[0];
        setError(first ?? tr.settings.saveError);
      } else {
        setError(tr.settings.saveError);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-3">
      <FormError message={error} />
      {message ? (
        <p role="status" className="rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300">
          {message}
        </p>
      ) : null}
      {hasPassword ? (
        <Field label={tr.settings.currentPassword} htmlFor="current-password">
          <input
            id="current-password"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            className={inputClass}
          />
        </Field>
      ) : (
        <p className="text-sm text-slate-600 dark:text-slate-400">{tr.settings.setPasswordHelp}</p>
      )}
      <Field label={tr.settings.newPassword} htmlFor="new-password" error={errors.newPassword}>
        <input
          id="new-password"
          type="password"
          autoComplete="new-password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          className={inputClass}
        />
      </Field>
      <div>
        <button type="submit" disabled={busy} className={primaryButtonClass}>
          {busy
            ? tr.common.loading
            : hasPassword
              ? tr.settings.changePasswordButton
              : tr.settings.setPasswordButton}
        </button>
      </div>
    </form>
  );
}

function DeleteSection() {
  const { logout } = useAuth();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await authApi.deleteAccount(password);
      await logout();
    } catch (err) {
      setBusy(false);
      setError(
        err instanceof ApiError && err.status === 401
          ? tr.settings.currentPasswordWrong
          : tr.settings.saveError,
      );
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3">
      <p className="text-sm text-slate-600 dark:text-slate-400">{tr.settings.deleteWarning}</p>
      <FormError message={error} />
      <Field label={tr.settings.deleteConfirmPassword} htmlFor="delete-password">
        <input
          id="delete-password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className={inputClass}
        />
      </Field>
      <div>
        <button
          type="submit"
          disabled={busy || password.length === 0}
          className={dangerButtonClass}
        >
          {busy ? tr.common.loading : tr.settings.deleteButton}
        </button>
      </div>
    </form>
  );
}

export default function SettingsPage() {
  const { user } = useAuth();

  return (
    <div className="flex flex-col gap-8">
      <h2 className="text-xl font-semibold">{tr.settings.title}</h2>

      <section aria-labelledby="profile-title" className="flex flex-col gap-3">
        <h3 id="profile-title" className="text-lg font-semibold">
          {tr.settings.profile}
        </h3>
        <p className="text-sm text-slate-600 dark:text-slate-400">{user?.email}</p>
        {user?.linkedGoogle ? (
          <p className="text-sm text-slate-600 dark:text-slate-400">🔗 {tr.settings.googleLinked}</p>
        ) : null}
        <TimezoneSection />
      </section>

      <section aria-labelledby="reminders-title" className="flex flex-col gap-3">
        <h3 id="reminders-title" className="text-lg font-semibold">
          {tr.settings.reminders}
        </h3>
        <ReminderSettings />
      </section>

      <section aria-labelledby="password-title" className="flex flex-col gap-3">
        <h3 id="password-title" className="text-lg font-semibold">
          {user?.hasPassword === false ? tr.settings.setPassword : tr.settings.changePassword}
        </h3>
        <PasswordSection hasPassword={user?.hasPassword !== false} />
      </section>

      <section
        aria-labelledby="danger-title"
        className="flex flex-col gap-3 rounded-xl border border-red-200 dark:border-red-900 p-4"
      >
        <h3 id="danger-title" className="text-lg font-semibold text-red-700 dark:text-red-400">
          {tr.settings.dangerZone}
        </h3>
        <DeleteSection />
      </section>
    </div>
  );
}
