"use client";

import { use, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { friendsApi, groupsApi, habitsApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import type { FriendDto, GroupDetailDto, GroupMemberProgressDto, HabitDto } from "@/lib/types";
import {
  ErrorState,
  Field,
  FormError,
  Skeleton,
  dangerButtonClass,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
} from "@/components/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; group: GroupDetailDto; friends: FriendDto[]; habits: HabitDto[] };

function MemberRow({
  member,
  canRemove,
  onRemove,
}: {
  member: GroupMemberProgressDto;
  canRemove: boolean;
  onRemove: () => void;
}) {
  const unit = member.streakUnit === "weeks" ? tr.dashboard.weeks : tr.dashboard.days;
  const status = member.completedToday
    ? tr.groups.doneToday
    : member.scheduledToday
      ? tr.groups.notDoneToday
      : tr.groups.notScheduledToday;

  return (
    <li className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <span
        aria-hidden="true"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-100 dark:bg-slate-800 text-lg"
      >
        {member.habitIcon}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">
          {member.email}
          {member.isYou ? ` (${tr.groups.you})` : ""}
          {member.isOwner ? ` · ${tr.groups.owner}` : ""}
        </p>
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {member.habitName} · 🔥 {member.currentStreak} {unit} · {status} ·{" "}
          {tr.groups.last7(member.completionsLast7Days)}
        </p>
      </div>
      {canRemove ? (
        <button
          type="button"
          onClick={onRemove}
          className="shrink-0 text-sm font-medium text-slate-500 hover:underline"
        >
          {tr.groups.removeMember}
        </button>
      ) : null}
    </li>
  );
}

/**
 * Split from the route component so the view can be rendered with a plain id.
 * The route itself unwraps the params promise with use(), which only resolves
 * under the router's own Suspense boundary.
 */
export function GroupDetailView({ groupId: id }: { groupId: string }) {
  const router = useRouter();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [actionError, setActionError] = useState<string | null>(null);
  const [inviteId, setInviteId] = useState("");

  const load = useCallback(() => {
    Promise.all([groupsApi.get(id), friendsApi.list(), habitsApi.list()])
      .then(([group, friends, habits]) =>
        setState({ kind: "ready", group, friends, habits: habits.items }),
      )
      .catch(() => setState({ kind: "error" }));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function act(action: () => Promise<void>, thenLeave = false) {
    setActionError(null);
    try {
      await action();
      if (thenLeave) {
        router.replace("/gruplar");
      } else {
        load();
      }
    } catch {
      setActionError(tr.groups.actionError);
    }
  }

  if (state.kind === "loading") {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tr.common.loading}>
        <Skeleton className="h-8 w-56" />
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

  const { group, friends, habits } = state;
  const activeHabits = habits.filter((h) => !h.isArchived);
  const alreadyInGroup = new Set(group.members.map((m) => m.userId));
  for (const invitee of group.pendingInvitees) alreadyInGroup.add(invitee.userId);
  const invitable = friends.filter((f) => !alreadyInGroup.has(f.userId));

  return (
    <div className="flex flex-col gap-6">
      <Link href="/gruplar" className="text-sm font-medium text-sky-600 hover:underline">
        ← {tr.groups.back}
      </Link>

      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-12 w-12 items-center justify-center rounded-full text-2xl"
          style={{ backgroundColor: `${group.color}22` }}
        >
          {group.icon}
        </span>
        <div>
          <h2 className="text-xl font-semibold">{group.name}</h2>
          {group.description ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">{group.description}</p>
          ) : null}
        </div>
      </div>

      <FormError message={actionError} />

      <section aria-labelledby="members-title" className="flex flex-col gap-3">
        <h3 id="members-title" className="text-lg font-semibold">
          {tr.groups.members}
        </h3>
        <ul className="flex flex-col gap-2">
          {group.members.map((member) => (
            <MemberRow
              key={member.userId}
              member={member}
              canRemove={group.isOwner && !member.isYou}
              onRemove={() => void act(() => groupsApi.removeMember(group.id, member.userId))}
            />
          ))}
        </ul>
      </section>

      {activeHabits.length > 0 ? (
        <section aria-labelledby="habit-title" className="flex flex-col gap-3">
          <h3 id="habit-title" className="text-lg font-semibold">
            {tr.groups.changeHabit}
          </h3>
          <Field label={tr.groups.linkedHabit} htmlFor="group-habit-change">
            <select
              id="group-habit-change"
              defaultValue=""
              onChange={(e) => {
                if (e.target.value) {
                  void act(() => groupsApi.changeHabit(group.id, e.target.value));
                }
              }}
              className={inputClass}
            >
              <option value="">—</option>
              {activeHabits.map((habit) => (
                <option key={habit.id} value={habit.id}>
                  {habit.icon} {habit.name}
                </option>
              ))}
            </select>
          </Field>
        </section>
      ) : null}

      {group.isOwner ? (
        <section aria-labelledby="invite-title" className="flex flex-col gap-3">
          <h3 id="invite-title" className="text-lg font-semibold">
            {tr.groups.invite}
          </h3>
          {invitable.length === 0 ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">{tr.groups.inviteNoFriends}</p>
          ) : (
            <div className="flex flex-wrap items-end gap-2">
              <div className="flex min-w-56 flex-1 flex-col gap-1">
                <label htmlFor="invite-friend" className="text-sm font-medium">
                  {tr.groups.inviteSelect}
                </label>
                <select
                  id="invite-friend"
                  value={inviteId}
                  onChange={(e) => setInviteId(e.target.value)}
                  className={inputClass}
                >
                  <option value="">—</option>
                  {invitable.map((friend) => (
                    <option key={friend.userId} value={friend.userId}>
                      {friend.email}
                    </option>
                  ))}
                </select>
              </div>
              <button
                type="button"
                disabled={!inviteId}
                onClick={() =>
                  void act(async () => {
                    await groupsApi.invite(group.id, inviteId);
                    setInviteId("");
                  })
                }
                className={primaryButtonClass}
              >
                {tr.groups.inviteSend}
              </button>
            </div>
          )}

          {group.pendingInvitees.length > 0 ? (
            <div>
              <p className="text-sm font-medium">{tr.groups.pendingInvitees}</p>
              <ul className="mt-1 flex flex-col gap-1">
                {group.pendingInvitees.map((invitee) => (
                  <li key={invitee.userId} className="text-sm text-slate-500 dark:text-slate-400">
                    {invitee.email}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </section>
      ) : null}

      <div>
        {group.isOwner ? (
          <button
            type="button"
            onClick={() => void act(() => groupsApi.remove(group.id), true)}
            className={dangerButtonClass}
          >
            {tr.groups.deleteGroup}
          </button>
        ) : (
          <button
            type="button"
            onClick={() => void act(() => groupsApi.leave(group.id), true)}
            className={secondaryButtonClass}
          >
            {tr.groups.leave}
          </button>
        )}
      </div>
    </div>
  );
}

export default function GroupDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <GroupDetailView groupId={id} />;
}
