"use client";

import { useEffect, useState } from "react";
import { authApi } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";

/** Renders nothing when the server has no Google credentials configured. */
export function GoogleSignInButton({ returnPath = "/" }: { returnPath?: string }) {
  const [available, setAvailable] = useState<boolean | null>(null);

  useEffect(() => {
    let cancelled = false;
    authApi
      .googleAvailable()
      .then((result) => {
        if (!cancelled) setAvailable(result);
      })
      .catch(() => {
        if (!cancelled) setAvailable(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (available !== true) return null;

  return (
    <>
      <div className="flex items-center gap-3" aria-hidden="true">
        <span className="h-px flex-1 bg-slate-200 dark:bg-slate-700" />
        <span className="text-xs text-slate-500 dark:text-slate-400">{tr.auth.or}</span>
        <span className="h-px flex-1 bg-slate-200 dark:bg-slate-700" />
      </div>
      <button
        type="button"
        onClick={() => authApi.startGoogleSignIn(returnPath)}
        className="flex w-full items-center justify-center gap-2 rounded-lg border border-slate-300 dark:border-slate-700 px-4 py-2 text-sm font-medium hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500"
      >
        <svg aria-hidden="true" viewBox="0 0 18 18" className="h-4 w-4">
          <path
            fill="#4285F4"
            d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.91c1.7-1.57 2.69-3.88 2.69-6.62Z"
          />
          <path
            fill="#34A853"
            d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.91-2.26c-.81.54-1.84.86-3.05.86-2.35 0-4.34-1.58-5.05-3.71H.92v2.33A9 9 0 0 0 9 18Z"
          />
          <path
            fill="#FBBC05"
            d="M3.95 10.71a5.41 5.41 0 0 1 0-3.42V4.96H.92a9 9 0 0 0 0 8.08l3.03-2.33Z"
          />
          <path
            fill="#EA4335"
            d="M9 3.58c1.32 0 2.51.45 3.44 1.35l2.58-2.59C13.46.89 11.43 0 9 0A9 9 0 0 0 .92 4.96l3.03 2.33C4.66 5.16 6.65 3.58 9 3.58Z"
          />
        </svg>
        {tr.auth.googleSignIn}
      </button>
    </>
  );
}
