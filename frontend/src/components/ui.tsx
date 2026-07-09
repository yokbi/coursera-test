"use client";

/** Small shared primitives: consistent focus states, dark-mode aware, accessible. */

export function Field({
  label,
  htmlFor,
  error,
  children,
}: {
  label: string;
  htmlFor: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {error ? (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : null}
    </div>
  );
}

export const inputClass =
  "rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-2 text-sm " +
  "focus:outline-2 focus:outline-offset-1 focus:outline-sky-500 disabled:opacity-50 w-full";

export const primaryButtonClass =
  "rounded-lg bg-sky-600 text-white px-4 py-2 text-sm font-medium hover:bg-sky-700 " +
  "focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 disabled:opacity-50 disabled:cursor-not-allowed";

export const secondaryButtonClass =
  "rounded-lg border border-slate-300 dark:border-slate-700 px-4 py-2 text-sm font-medium " +
  "hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 " +
  "disabled:opacity-50 disabled:cursor-not-allowed";

export const dangerButtonClass =
  "rounded-lg bg-red-600 text-white px-4 py-2 text-sm font-medium hover:bg-red-700 " +
  "focus:outline-2 focus:outline-offset-2 focus:outline-red-500 disabled:opacity-50 disabled:cursor-not-allowed";

export function FormError({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <p
      role="alert"
      className="rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 px-3 py-2 text-sm text-red-700 dark:text-red-300"
    >
      {message}
    </p>
  );
}

export function Skeleton({ className = "" }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={`animate-pulse rounded-lg bg-slate-200 dark:bg-slate-800 ${className}`}
    />
  );
}

export function EmptyState({
  message,
  action,
}: {
  message: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-slate-300 dark:border-slate-700 px-6 py-10 text-center">
      <p className="text-sm text-slate-600 dark:text-slate-400">{message}</p>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry, retryLabel }: {
  message: string;
  onRetry: () => void;
  retryLabel: string;
}) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-xl border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950/40 px-6 py-10 text-center"
    >
      <p className="text-sm text-red-700 dark:text-red-300">{message}</p>
      <button type="button" onClick={onRetry} className={secondaryButtonClass}>
        {retryLabel}
      </button>
    </div>
  );
}
