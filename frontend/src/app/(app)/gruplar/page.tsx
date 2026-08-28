"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { groupsApi, habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { GroupSummaryDto, HabitDto } from "@/lib/types";
import {
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Skeleton,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
} from "@/components/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; groups: GroupSummaryDto[]; habits: HabitDto[] };

function GroupCard({ group }: { group: GroupSummaryDto }) {
  return (
    <li className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <Link href={`/gruplar/${group.id}`} className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-lg"
          style={{ backgroundColor: `${group.color}22` }}
        >
          {group.icon}
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate font-medium">{group.name}</p>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {tr.groups.memberCount(group.memberCount)}
            {group.isOwner ? ` · ${tr.groups.owner}` : ""}
          </p>
        </div>
      </Link>
    </li>
  );
}

export default function GroupsPage() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [habitId, setHabitId] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    Promise.all([groupsApi.list(), habitsApi.list()])
      .then(([groups, habits]) => setState({ kind: "ready", groups, habits: habits.items }))
      .catch(() => setState({ kind: "error" }));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!name.trim()) nextErrors.name = tr.groups.nameRequired;
    if (!habitId) nextErrors.habitId = tr.groups.habitRequired;
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setBusy(true);
    setFormError(null);
    try {
      await groupsApi.create({
        name: name.trim(),
        description: description.trim() || null,
        color: "#22c55e",
        icon: "👥",
        habitId,
      });
      setName("");
      setDescription("");
      setHabitId("");
      setCreating(false);
      load();
    } catch {
      setFormError(tr.groups.actionError);
    } finally {
      setBusy(false);
    }
  }

  async function respond(groupId: string, action: () => Promise<void>) {
    setFormError(null);
    try {
      await action();
      load();
    } catch {
      setFormError(tr.groups.actionError);
    }
  }

  if (state.kind === "loading") {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tr.common.loading}>
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.groups.loadError}
        onRetry={() => {
          setState({ kind: "loading" });
          load();
        }}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  const invitations = state.groups.filter((g) => !g.joined);
  const joined = state.groups.filter((g) => g.joined);
  const activeHabits = state.habits.filter((h) => !h.isArchived);

  return (
    <div className="flex flex-col gap-8">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">{tr.groups.title}</h2>
          <p className="mt-1 max-w-2xl text-sm text-slate-500 dark:text-slate-400">
            {tr.groups.subtitle}
          </p>
        </div>
        {!creating ? (
          <button type="button" onClick={() => setCreating(true)} className={primaryButtonClass}>
            {tr.groups.newGroup}
          </button>
        ) : null}
      </div>

      <FormError message={formError} />

      {creating ? (
        <form
          onSubmit={submit}
          noValidate
          aria-label={tr.groups.newGroup}
          className="flex flex-col gap-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4"
        >
          <Field label={tr.groups.name} htmlFor="group-name" error={errors.name}>
            <input
              id="group-name"
              value={name}
              maxLength={100}
              onChange={(e) => setName(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label={tr.groups.description} htmlFor="group-description">
            <textarea
              id="group-description"
              value={description}
              maxLength={500}
              rows={2}
              onChange={(e) => setDescription(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label={tr.groups.linkedHabit} htmlFor="group-habit" error={errors.habitId}>
            {activeHabits.length === 0 ? (
              <p className="text-sm text-slate-500 dark:text-slate-400">{tr.groups.noHabits}</p>
            ) : (
              <select
                id="group-habit"
                value={habitId}
                onChange={(e) => setHabitId(e.target.value)}
                className={inputClass}
              >
                <option value="">—</option>
                {activeHabits.map((habit) => (
                  <option key={habit.id} value={habit.id}>
                    {habit.icon} {habit.name}
                  </option>
                ))}
              </select>
            )}
          </Field>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={busy || activeHabits.length === 0}
              className={primaryButtonClass}
            >
              {busy ? tr.common.loading : tr.groups.create}
            </button>
            <button type="button" onClick={() => setCreating(false)} className={secondaryButtonClass}>
              {tr.groups.cancel}
            </button>
          </div>
        </form>
      ) : null}

      {invitations.length > 0 ? (
        <section aria-labelledby="invites-title" className="flex flex-col gap-3">
          <h3 id="invites-title" className="text-lg font-semibold">
            {tr.groups.invitations}
          </h3>
          <ul className="flex flex-col gap-2">
            {invitations.map((group) => (
              <li
                key={group.id}
                className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3"
              >
                <span className="min-w-0 flex-1 truncate text-sm font-medium">
                  {group.icon} {group.name}
                </span>
                {activeHabits.length > 0 ? (
                  <select
                    aria-label={tr.groups.linkedHabit}
                    defaultValue=""
                    onChange={(e) => {
                      if (e.target.value) {
                        void respond(group.id, () => groupsApi.join(group.id, e.target.value));
                      }
                    }}
                    className={`${inputClass} w-auto`}
                  >
                    <option value="">{tr.groups.join}…</option>
                    {activeHabits.map((habit) => (
                      <option key={habit.id} value={habit.id}>
                        {habit.icon} {habit.name}
                      </option>
                    ))}
                  </select>
                ) : (
                  <span className="text-xs text-slate-500">{tr.groups.noHabits}</span>
                )}
                <button
                  type="button"
                  onClick={() => void respond(group.id, () => groupsApi.decline(group.id))}
                  className={secondaryButtonClass}
                >
                  {tr.groups.decline}
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {joined.length === 0 ? (
        <EmptyState message={tr.groups.empty} />
      ) : (
        <ul className="flex flex-col gap-2">
          {joined.map((group) => (
            <GroupCard key={group.id} group={group} />
          ))}
        </ul>
      )}
    </div>
  );
}
