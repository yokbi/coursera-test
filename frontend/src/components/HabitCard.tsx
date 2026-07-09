"use client";

import { useState } from "react";
import { ApiError, habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { TodayHabitDto } from "@/lib/types";

function localToday(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** One dashboard row: completion state, check-in controls, streak badge. */
export function HabitCard({ entry }: { entry: TodayHabitDto }) {
  const [state, setState] = useState(entry);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { habit } = entry;
  const target = habit.targetValue ?? 1;

  async function applyCheckIn(value: number, increment: boolean) {
    setBusy(true);
    setError(null);
    try {
      const result = await habitsApi.upsertCheckIn(habit.id, localToday(), value, increment);
      setState((previous) => ({
        ...previous,
        todayValue: result.checkIn.value,
        completedToday: result.checkIn.completed,
        currentStreak: result.currentStreak,
        streakUnit: result.streakUnit,
      }));
    } catch (err) {
      setError(err instanceof ApiError && err.status === 422
        ? tr.checkin.editWindowError
        : tr.habits.saveError);
    } finally {
      setBusy(false);
    }
  }

  const streakLabel = `${state.currentStreak} ${
    state.streakUnit === "weeks" ? tr.dashboard.weeks : tr.dashboard.days
  }`;

  return (
    <li
      className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4"
      data-testid={`habit-card-${habit.id}`}
    >
      <span
        aria-hidden="true"
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-lg"
        style={{ backgroundColor: `${habit.color}22` }}
      >
        {habit.icon}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{habit.name}</p>
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {state.currentStreak > 0 ? `🔥 ${streakLabel} ${tr.dashboard.streak}` : habit.category ?? ""}
          {habit.scheduleType === "timesPerWeek" && habit.timesPerWeek
            ? ` · ${tr.dashboard.weekProgress(state.weekCompletions, habit.timesPerWeek)}`
            : ""}
        </p>
        {error ? (
          <p role="alert" className="mt-1 text-xs text-red-600 dark:text-red-400">
            {error}
          </p>
        ) : null}
      </div>

      {habit.type === "boolean" ? (
        <button
          type="button"
          disabled={busy}
          aria-pressed={state.completedToday}
          aria-label={`${habit.name}: ${state.completedToday ? tr.checkin.done : tr.checkin.notDone}`}
          onClick={() => void applyCheckIn(state.completedToday ? 0 : 1, false)}
          className={`h-10 w-10 shrink-0 rounded-full border text-lg focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 disabled:opacity-50 ${
            state.completedToday
              ? "border-emerald-500 bg-emerald-500 text-white"
              : "border-slate-300 dark:border-slate-600 hover:border-emerald-400"
          }`}
        >
          {state.completedToday ? "✓" : ""}
        </button>
      ) : (
        <div className="flex shrink-0 items-center gap-2">
          <button
            type="button"
            disabled={busy || state.todayValue <= 0}
            aria-label={`${habit.name}: ${tr.checkin.decrement}`}
            onClick={() => void applyCheckIn(-step(habit.type), true)}
            className="h-9 w-9 rounded-lg border border-slate-300 dark:border-slate-600 text-lg hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 disabled:opacity-40"
          >
            −
          </button>
          <span
            className={`min-w-16 text-center text-sm tabular-nums ${
              state.completedToday ? "font-semibold text-emerald-600 dark:text-emerald-400" : ""
            }`}
            aria-live="polite"
          >
            {state.todayValue}/{target}{" "}
            {habit.type === "duration" ? tr.dashboard.minutes : habit.unit ?? ""}
          </span>
          <button
            type="button"
            disabled={busy}
            aria-label={`${habit.name}: ${tr.checkin.increment}`}
            onClick={() => void applyCheckIn(step(habit.type), true)}
            className="h-9 w-9 rounded-lg border border-slate-300 dark:border-slate-600 text-lg hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 disabled:opacity-40"
          >
            +
          </button>
        </div>
      )}
    </li>
  );
}

function step(type: TodayHabitDto["habit"]["type"]): number {
  return type === "duration" ? 5 : 1;
}
