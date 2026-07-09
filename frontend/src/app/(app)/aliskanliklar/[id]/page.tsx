"use client";

import { use, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { HabitDto, HabitStatsDto } from "@/lib/types";
import { Heatmap } from "@/components/Heatmap";
import { ErrorState, Skeleton } from "@/components/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; habit: HabitDto; stats: HabitStatsDto };

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <dt className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 text-2xl font-bold tabular-nums">{value}</dd>
    </div>
  );
}

export default function HabitStatsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  // setState lives only in promise callbacks so the effect never triggers a
  // synchronous state update (react-hooks/set-state-in-effect).
  const load = useCallback(() => {
    Promise.all([habitsApi.get(id), habitsApi.stats(id)])
      .then(([habit, stats]) => setState({ kind: "ready", habit, stats }))
      .catch(() => setState({ kind: "error" }));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const retry = useCallback(() => {
    setState({ kind: "loading" });
    load();
  }, [load]);

  if (state.kind === "loading") {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tr.common.loading}>
        <Skeleton className="h-8 w-56" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
        </div>
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.stats.loadError}
        onRetry={retry}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  const { habit, stats } = state;
  const unit = stats.streakUnit === "weeks" ? tr.dashboard.weeks : tr.dashboard.days;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-3">
        <Link
          href="/aliskanliklar"
          className="text-sm font-medium text-sky-600 hover:underline"
        >
          ← {tr.stats.back}
        </Link>
      </div>

      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-12 w-12 items-center justify-center rounded-full text-2xl"
          style={{ backgroundColor: `${habit.color}22` }}
        >
          {habit.icon}
        </span>
        <div>
          <h2 className="text-xl font-semibold">{habit.name}</h2>
          {habit.description ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">{habit.description}</p>
          ) : null}
        </div>
      </div>

      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard
          label={tr.stats.currentStreak}
          value={`${stats.currentStreak} ${unit}`}
        />
        <StatCard
          label={tr.stats.longestStreak}
          value={`${stats.longestStreak} ${unit}`}
        />
        <StatCard
          label={tr.stats.completionRate30}
          value={`%${Math.round(stats.completionRate30 * 100)}`}
        />
        <StatCard
          label={tr.stats.completionRate90}
          value={`%${Math.round(stats.completionRate90 * 100)}`}
        />
      </dl>

      <section aria-labelledby="heatmap-title" className="flex flex-col gap-3">
        <h3 id="heatmap-title" className="text-lg font-semibold">
          {tr.stats.heatmapTitle}
        </h3>
        <Heatmap days={stats.heatmap90} color={habit.color} />
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {tr.stats.totalCheckIns}: {stats.totalCheckIns}
        </p>
      </section>
    </div>
  );
}
