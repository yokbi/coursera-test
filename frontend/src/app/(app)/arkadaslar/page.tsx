"use client";

import { useCallback, useEffect, useState } from "react";
import { friendsApi } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { tr } from "@/lib/i18n/tr";
import type { FriendDto, FriendRequestsDto } from "@/lib/types";
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
  | { kind: "ready"; friends: FriendDto[]; requests: FriendRequestsDto };

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function SharingToggle() {
  const { user, setUser } = useAuth();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const enabled = user?.shareStreaksWithFriends ?? false;

  async function toggle() {
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      setUser(await friendsApi.updateSharing(!enabled));
      setMessage(tr.friends.sharingSaved);
    } catch {
      setError(tr.friends.actionError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <div className="flex items-start gap-2">
        <input
          id="share-streaks"
          type="checkbox"
          checked={enabled}
          disabled={busy}
          onChange={() => void toggle()}
          className="mt-1 h-4 w-4 accent-sky-600 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500"
        />
        <label htmlFor="share-streaks" className="text-sm font-medium">
          {tr.friends.sharingToggle}
        </label>
      </div>
      <p className="text-xs text-slate-500 dark:text-slate-400">{tr.friends.sharingHelp}</p>
      <FormError message={error} />
      {message ? (
        <p role="status" className="text-xs text-emerald-700 dark:text-emerald-300">
          {message}
        </p>
      ) : null}
    </div>
  );
}

function FriendCard({ friend, onRemove }: { friend: FriendDto; onRemove: () => void }) {
  const unit = friend.bestStreakUnit === "weeks" ? tr.dashboard.weeks : tr.dashboard.days;

  return (
    <li className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{friend.email}</p>
        {friend.sharingEnabled ? (
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {friend.bestStreak && friend.bestStreak > 0
              ? `🔥 ${friend.bestStreak} ${unit} — ${friend.bestStreakHabitName}`
              : `${friend.activeHabits} ${tr.friends.activeHabits}`}
            {friend.scheduledToday && friend.scheduledToday > 0
              ? ` · ${tr.friends.todayProgress(friend.completedToday ?? 0, friend.scheduledToday)}`
              : ""}
          </p>
        ) : (
          <p className="text-xs text-slate-400 dark:text-slate-500">{tr.friends.notSharing}</p>
        )}
      </div>
      <button
        type="button"
        onClick={onRemove}
        className="shrink-0 text-sm font-medium text-slate-500 hover:underline"
      >
        {tr.friends.remove}
      </button>
    </li>
  );
}

export default function FriendsPage() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const load = useCallback(() => {
    Promise.all([friendsApi.list(), friendsApi.requests()])
      .then(([friends, requests]) => setState({ kind: "ready", friends, requests }))
      .catch(() => setState({ kind: "error" }));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function sendRequest(event: React.FormEvent) {
    event.preventDefault();
    setNotice(null);
    setFormError(null);

    const trimmed = email.trim();
    if (!EMAIL_PATTERN.test(trimmed)) {
      setEmailError(tr.friends.emailInvalid);
      return;
    }
    setEmailError(null);
    setSending(true);
    try {
      await friendsApi.sendRequest(trimmed);
      setEmail("");
      setNotice(tr.friends.sent);
      load();
    } catch {
      setFormError(tr.friends.sendError);
    } finally {
      setSending(false);
    }
  }

  async function act(action: () => Promise<void>) {
    setFormError(null);
    try {
      await action();
      load();
    } catch {
      setFormError(tr.friends.actionError);
    }
  }

  if (state.kind === "loading") {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tr.common.loading}>
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.friends.loadError}
        onRetry={() => {
          setState({ kind: "loading" });
          load();
        }}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  const { friends, requests } = state;

  return (
    <div className="flex flex-col gap-8">
      <h2 className="text-xl font-semibold">{tr.friends.title}</h2>

      <section aria-labelledby="sharing-title" className="flex flex-col gap-3">
        <h3 id="sharing-title" className="text-lg font-semibold">
          {tr.friends.sharingTitle}
        </h3>
        <SharingToggle />
      </section>

      <section aria-labelledby="add-title" className="flex flex-col gap-3">
        <h3 id="add-title" className="text-lg font-semibold">
          {tr.friends.addTitle}
        </h3>
        <form onSubmit={sendRequest} noValidate className="flex flex-col gap-3">
          <FormError message={formError} />
          {notice ? (
            <p
              role="status"
              className="rounded-lg bg-sky-50 dark:bg-sky-950/40 border border-sky-200 dark:border-sky-900 px-3 py-2 text-sm text-sky-800 dark:text-sky-300"
            >
              {notice}
            </p>
          ) : null}
          <Field label={tr.friends.emailLabel} htmlFor="friend-email" error={emailError ?? undefined}>
            <input
              id="friend-email"
              type="email"
              value={email}
              placeholder={tr.friends.emailPlaceholder}
              onChange={(e) => setEmail(e.target.value)}
              className={inputClass}
            />
          </Field>
          <div>
            <button type="submit" disabled={sending} className={primaryButtonClass}>
              {sending ? tr.common.loading : tr.friends.send}
            </button>
          </div>
        </form>
      </section>

      {requests.incoming.length > 0 ? (
        <section aria-labelledby="incoming-title" className="flex flex-col gap-3">
          <h3 id="incoming-title" className="text-lg font-semibold">
            {tr.friends.incoming}
          </h3>
          <ul className="flex flex-col gap-2">
            {requests.incoming.map((request) => (
              <li
                key={request.requestId}
                className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3"
              >
                <span className="min-w-0 flex-1 truncate text-sm">{request.email}</span>
                <button
                  type="button"
                  onClick={() => void act(() => friendsApi.accept(request.requestId))}
                  className={primaryButtonClass}
                >
                  {tr.friends.accept}
                </button>
                <button
                  type="button"
                  onClick={() => void act(() => friendsApi.decline(request.requestId))}
                  className={secondaryButtonClass}
                >
                  {tr.friends.decline}
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {requests.outgoing.length > 0 ? (
        <section aria-labelledby="outgoing-title" className="flex flex-col gap-3">
          <h3 id="outgoing-title" className="text-lg font-semibold">
            {tr.friends.outgoing}
          </h3>
          <ul className="flex flex-col gap-2">
            {requests.outgoing.map((request) => (
              <li
                key={request.requestId}
                className="flex items-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3"
              >
                <span className="min-w-0 flex-1 truncate text-sm">{request.email}</span>
                <span className="shrink-0 text-xs text-slate-500 dark:text-slate-400">
                  {tr.friends.pending}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="list-title" className="flex flex-col gap-3">
        <h3 id="list-title" className="text-lg font-semibold">
          {tr.friends.title}
        </h3>
        {friends.length === 0 ? (
          <EmptyState message={tr.friends.empty} />
        ) : (
          <ul className="flex flex-col gap-2">
            {friends.map((friend) => (
              <FriendCard
                key={friend.userId}
                friend={friend}
                onRemove={() => void act(() => friendsApi.remove(friend.userId))}
              />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
