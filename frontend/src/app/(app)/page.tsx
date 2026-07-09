"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { TodayHabitDto, WeekOverviewDto } from "@/lib/types";
import { HabitCard } from "@/components/HabitCard";
import { WeekGrid } from "@/components/WeekGrid";
import { EmptyState, ErrorState, Skeleton, primaryButtonClass } from "@/components/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; today: TodayHabitDto[]; week: WeekOverviewDto };

export default function DashboardPage() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  // setState lives only in promise callbacks so the effect never triggers a
  // synchronous state update (react-hooks/set-state-in-effect).
  const load = useCallback(() => {
    Promise.all([habitsApi.today(), habitsApi.week()])
      .then(([today, week]) => setState({ kind: "ready", today, week }))
      .catch(() => setState({ kind: "error" }));
  }, []);

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
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.dashboard.error}
        onRetry={retry}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="today-title" className="flex flex-col gap-4">
        <h2 id="today-title" className="text-xl font-semibold">
          {tr.dashboard.todayTitle}
        </h2>
        {state.today.length === 0 ? (
          <EmptyState
            message={tr.dashboard.empty}
            action={
              <Link href="/aliskanliklar?yeni=1" className={primaryButtonClass}>
                {tr.dashboard.emptyCta}
              </Link>
            }
          />
        ) : (
          <ul className="flex flex-col gap-3">
            {state.today.map((entry) => (
              <HabitCard key={entry.habit.id} entry={entry} />
            ))}
          </ul>
        )}
      </section>

      {state.week.habits.length > 0 ? (
        <section aria-labelledby="week-title" className="flex flex-col gap-4">
          <h2 id="week-title" className="text-xl font-semibold">
            {tr.dashboard.weekTitle}
          </h2>
          <WeekGrid week={state.week} />
        </section>
      ) : null}
    </div>
  );
}
