"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { HabitDto } from "@/lib/types";
import { HabitForm } from "@/components/HabitForm";
import {
  EmptyState,
  ErrorState,
  Skeleton,
  primaryButtonClass,
  secondaryButtonClass,
} from "@/components/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; habits: HabitDto[] };

function HabitRow({
  habit,
  index,
  count,
  onEdit,
  onChanged,
  onMove,
}: {
  habit: HabitDto;
  index: number;
  count: number;
  onEdit: () => void;
  onChanged: (updated: HabitDto) => void;
  onMove: (from: number, to: number) => void;
}) {
  const [busy, setBusy] = useState(false);

  async function toggleArchive() {
    setBusy(true);
    try {
      const updated = habit.isArchived
        ? await habitsApi.unarchive(habit.id)
        : await habitsApi.archive(habit.id);
      onChanged(updated);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3">
      {!habit.isArchived ? (
        <div className="flex flex-col">
          <button
            type="button"
            aria-label={`${habit.name}: ${tr.habits.moveUp}`}
            disabled={index === 0}
            onClick={() => onMove(index, index - 1)}
            className="px-1 text-xs leading-4 text-slate-500 hover:text-slate-900 dark:hover:text-white disabled:opacity-30"
          >
            ▲
          </button>
          <button
            type="button"
            aria-label={`${habit.name}: ${tr.habits.moveDown}`}
            disabled={index === count - 1}
            onClick={() => onMove(index, index + 1)}
            className="px-1 text-xs leading-4 text-slate-500 hover:text-slate-900 dark:hover:text-white disabled:opacity-30"
          >
            ▼
          </button>
        </div>
      ) : null}
      <span
        aria-hidden="true"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-lg"
        style={{ backgroundColor: `${habit.color}22` }}
      >
        {habit.icon}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{habit.name}</p>
        {habit.description ? (
          <p className="truncate text-xs text-slate-500 dark:text-slate-400">
            {habit.description}
          </p>
        ) : null}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Link
          href={`/aliskanliklar/${habit.id}`}
          className="text-sm font-medium text-sky-600 hover:underline"
        >
          {tr.habits.stats}
        </Link>
        {!habit.isArchived ? (
          <button
            type="button"
            onClick={onEdit}
            className="text-sm font-medium text-sky-600 hover:underline"
          >
            {tr.habits.edit}
          </button>
        ) : null}
        <button
          type="button"
          disabled={busy}
          onClick={() => void toggleArchive()}
          className="text-sm font-medium text-slate-500 hover:underline disabled:opacity-50"
        >
          {habit.isArchived ? tr.habits.unarchive : tr.habits.archive}
        </button>
      </div>
    </li>
  );
}

function HabitsPageInner() {
  const searchParams = useSearchParams();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [editing, setEditing] = useState<HabitDto | null>(null);
  const [creating, setCreating] = useState(searchParams.get("yeni") === "1");
  const [showArchived, setShowArchived] = useState(false);

  // setState lives only in promise callbacks so the effect never triggers a
  // synchronous state update (react-hooks/set-state-in-effect).
  const load = useCallback(() => {
    habitsApi
      .list(true)
      .then((result) => setState({ kind: "ready", habits: result.items }))
      .catch(() => setState({ kind: "error" }));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const retry = useCallback(() => {
    setState({ kind: "loading" });
    load();
  }, [load]);

  async function moveHabit(from: number, to: number) {
    if (state.kind !== "ready") return;
    const active = state.habits.filter((h) => !h.isArchived);
    const reordered = [...active];
    const [moved] = reordered.splice(from, 1);
    reordered.splice(to, 0, moved);
    const archived = state.habits.filter((h) => h.isArchived);
    setState({ kind: "ready", habits: [...reordered, ...archived] });
    try {
      await habitsApi.reorder(reordered.map((h) => h.id));
    } catch {
      load(); // restore server order on failure
    }
  }

  function onSaved() {
    setEditing(null);
    setCreating(false);
    load();
  }

  if (state.kind === "loading") {
    return (
      <div className="flex flex-col gap-3" aria-busy="true" aria-label={tr.common.loading}>
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.habits.loadError}
        onRetry={retry}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  const active = state.habits.filter((h) => !h.isArchived);
  const archived = state.habits.filter((h) => h.isArchived);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-xl font-semibold">{tr.habits.title}</h2>
        {!creating && !editing ? (
          <button type="button" onClick={() => setCreating(true)} className={primaryButtonClass}>
            {tr.habits.newHabit}
          </button>
        ) : null}
      </div>

      {creating ? (
        <HabitForm onSaved={onSaved} onCancel={() => setCreating(false)} />
      ) : null}
      {editing ? (
        <HabitForm habit={editing} onSaved={onSaved} onCancel={() => setEditing(null)} />
      ) : null}

      {active.length === 0 && !creating ? (
        <EmptyState
          message={tr.habits.empty}
          action={
            <button type="button" onClick={() => setCreating(true)} className={primaryButtonClass}>
              {tr.habits.newHabit}
            </button>
          }
        />
      ) : (
        <ul className="flex flex-col gap-2">
          {active.map((habit, index) => (
            <HabitRow
              key={habit.id}
              habit={habit}
              index={index}
              count={active.length}
              onEdit={() => {
                setCreating(false);
                setEditing(habit);
              }}
              onChanged={() => load()}
              onMove={(from, to) => void moveHabit(from, to)}
            />
          ))}
        </ul>
      )}

      <div>
        <button
          type="button"
          onClick={() => setShowArchived((v) => !v)}
          aria-expanded={showArchived}
          className={secondaryButtonClass}
        >
          {tr.habits.archived} ({archived.length})
        </button>
        {showArchived ? (
          archived.length === 0 ? (
            <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
              {tr.habits.emptyArchived}
            </p>
          ) : (
            <ul className="mt-3 flex flex-col gap-2">
              {archived.map((habit, index) => (
                <HabitRow
                  key={habit.id}
                  habit={habit}
                  index={index}
                  count={archived.length}
                  onEdit={() => undefined}
                  onChanged={() => load()}
                  onMove={() => undefined}
                />
              ))}
            </ul>
          )
        ) : null}
      </div>
    </div>
  );
}

export default function HabitsPage() {
  return (
    <Suspense fallback={<Skeleton className="h-40 w-full" />}>
      <HabitsPageInner />
    </Suspense>
  );
}
