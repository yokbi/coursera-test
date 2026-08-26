"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { adminApi } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { tr } from "@/lib/i18n/tr";
import type { AdminMetricsDto, AdminUserDto } from "@/lib/types";
import {
  EmptyState,
  ErrorState,
  FormError,
  Skeleton,
  inputClass,
  secondaryButtonClass,
} from "@/components/ui";

const PAGE_SIZE = 25;

type LoadState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; metrics: AdminMetricsDto; users: AdminUserDto[]; total: number };

function MetricCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
      <dt className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 text-2xl font-bold tabular-nums">{value}</dd>
    </div>
  );
}

function StatusBadge({ user }: { user: AdminUserDto }) {
  const [label, classes] = user.isDeleted
    ? [tr.admin.statusDeleted, "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300"]
    : user.isSuspended
      ? [tr.admin.statusSuspended, "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300"]
      : [tr.admin.statusActive, "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"];

  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${classes}`}>{label}</span>;
}

export default function AdminPage() {
  const { user } = useAuth();
  const router = useRouter();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // The API enforces this; the redirect only avoids showing a shell that will 403.
  useEffect(() => {
    if (user && !user.isAdmin) {
      router.replace("/");
    }
  }, [user, router]);

  const load = useCallback(
    (term: string, targetPage: number) => {
      Promise.all([adminApi.metrics(), adminApi.listUsers(term, targetPage, PAGE_SIZE)])
        .then(([metrics, users]) =>
          setState({ kind: "ready", metrics, users: users.items, total: users.totalCount }),
        )
        .catch(() => setState({ kind: "error" }));
    },
    [],
  );

  useEffect(() => {
    load(search, page);
    // Re-running on `search` would fire a request per keystroke; the form submit drives it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [load, page]);

  function runSearch(event: React.FormEvent) {
    event.preventDefault();
    setState({ kind: "loading" });
    setPage(1);
    load(search, 1);
  }

  async function toggleSuspend(target: AdminUserDto) {
    setBusyId(target.id);
    setActionError(null);
    try {
      const updated = target.isSuspended
        ? await adminApi.unsuspend(target.id)
        : await adminApi.suspend(target.id);
      setState((previous) =>
        previous.kind === "ready"
          ? {
              ...previous,
              users: previous.users.map((u) => (u.id === updated.id ? updated : u)),
            }
          : previous,
      );
    } catch {
      setActionError(tr.admin.actionError);
    } finally {
      setBusyId(null);
    }
  }

  if (user && !user.isAdmin) return null;

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
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <ErrorState
        message={tr.admin.loadError}
        onRetry={() => {
          setState({ kind: "loading" });
          load(search, page);
        }}
        retryLabel={tr.dashboard.retry}
      />
    );
  }

  const totalPages = Math.max(1, Math.ceil(state.total / PAGE_SIZE));

  return (
    <div className="flex flex-col gap-8">
      <h2 className="text-xl font-semibold">{tr.admin.title}</h2>

      <section aria-labelledby="metrics-title" className="flex flex-col gap-3">
        <h3 id="metrics-title" className="text-lg font-semibold">
          {tr.admin.metrics}
        </h3>
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <MetricCard label={tr.admin.totalUsers} value={state.metrics.totalUsers} />
          <MetricCard label={tr.admin.activeUsers} value={state.metrics.activeUsers} />
          <MetricCard label={tr.admin.suspendedUsers} value={state.metrics.suspendedUsers} />
          <MetricCard label={tr.admin.newUsers30} value={state.metrics.newUsersLast30Days} />
          <MetricCard label={tr.admin.totalHabits} value={state.metrics.totalHabits} />
          <MetricCard label={tr.admin.archivedHabits} value={state.metrics.archivedHabits} />
          <MetricCard label={tr.admin.checkIns7} value={state.metrics.checkInsLast7Days} />
          <MetricCard label={tr.admin.remindersOn} value={state.metrics.usersWithRemindersOn} />
        </dl>
      </section>

      <section aria-labelledby="users-title" className="flex flex-col gap-3">
        <h3 id="users-title" className="text-lg font-semibold">
          {tr.admin.users}
        </h3>

        <form onSubmit={runSearch} className="flex flex-wrap items-end gap-2">
          <div className="flex min-w-56 flex-1 flex-col gap-1">
            <label htmlFor="admin-search" className="text-sm font-medium">
              {tr.admin.search}
            </label>
            <input
              id="admin-search"
              type="search"
              value={search}
              placeholder={tr.admin.searchPlaceholder}
              onChange={(e) => setSearch(e.target.value)}
              className={inputClass}
            />
          </div>
          <button type="submit" className={secondaryButtonClass}>
            {tr.admin.search}
          </button>
        </form>

        <FormError message={actionError} />

        {state.users.length === 0 ? (
          <EmptyState message={tr.admin.empty} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800">
                  <th scope="col" className="py-2 pr-3 font-medium">{tr.admin.email}</th>
                  <th scope="col" className="py-2 pr-3 font-medium">{tr.admin.role}</th>
                  <th scope="col" className="py-2 pr-3 font-medium">{tr.admin.status}</th>
                  <th scope="col" className="py-2 pr-3 font-medium">{tr.admin.habits}</th>
                  <th scope="col" className="py-2 pr-3 font-medium">{tr.admin.joined}</th>
                  <th scope="col" className="py-2 font-medium">{tr.admin.actions}</th>
                </tr>
              </thead>
              <tbody>
                {state.users.map((row) => (
                  <tr key={row.id} className="border-b border-slate-100 dark:border-slate-900">
                    <td className="py-2 pr-3">{row.email}</td>
                    <td className="py-2 pr-3">
                      {row.role === "admin" ? tr.admin.roleAdmin : tr.admin.roleUser}
                    </td>
                    <td className="py-2 pr-3">
                      <StatusBadge user={row} />
                    </td>
                    <td className="py-2 pr-3 tabular-nums">{row.habitCount}</td>
                    <td className="py-2 pr-3 tabular-nums">
                      {new Date(row.createdAt).toLocaleDateString("tr-TR")}
                    </td>
                    <td className="py-2">
                      {row.role === "admin" || row.isDeleted ? null : (
                        <button
                          type="button"
                          disabled={busyId === row.id}
                          onClick={() => void toggleSuspend(row)}
                          className="text-sm font-medium text-sky-600 hover:underline disabled:opacity-50"
                        >
                          {row.isSuspended ? tr.admin.unsuspend : tr.admin.suspend}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {totalPages > 1 ? (
          <div className="flex items-center gap-3">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => {
                setState({ kind: "loading" });
                setPage((p) => p - 1);
              }}
              className={secondaryButtonClass}
            >
              {tr.admin.previous}
            </button>
            <span className="text-sm text-slate-500 dark:text-slate-400">
              {tr.admin.pageInfo(page, totalPages)}
            </span>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => {
                setState({ kind: "loading" });
                setPage((p) => p + 1);
              }}
              className={secondaryButtonClass}
            >
              {tr.admin.next}
            </button>
          </div>
        ) : null}
      </section>
    </div>
  );
}
