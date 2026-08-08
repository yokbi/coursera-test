"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { tryRefresh } from "@/lib/api";
import { tr } from "@/lib/i18n/tr";
import { FormError, Skeleton } from "@/components/ui";

/**
 * Landing page for the Google callback. The backend has already set the refresh
 * cookie; exchanging it here also sets the marker cookie the route guard needs,
 * so the redirect into the app does not bounce back to login.
 */
function GoogleCallbackInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const next = searchParams.get("next");
    // Only same-origin relative paths, mirroring the backend's own check.
    const target = next?.startsWith("/") && !next.startsWith("//") ? next : "/";

    tryRefresh()
      .then((ok) => {
        if (cancelled) return;
        if (ok) {
          router.replace(target);
        } else {
          setFailed(true);
        }
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });

    return () => {
      cancelled = true;
    };
  }, [router, searchParams]);

  if (failed) {
    return (
      <div className="flex flex-col gap-4">
        <FormError message={tr.auth.googleError} />
        <a href="/giris" className="text-sm font-medium text-sky-600 hover:underline">
          {tr.auth.loginLink}
        </a>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3" aria-busy="true" aria-live="polite">
      <p className="text-sm text-slate-600 dark:text-slate-400">{tr.auth.googleSigningIn}</p>
      <Skeleton className="h-10 w-full" />
    </div>
  );
}

export default function GoogleCallbackPage() {
  return (
    <Suspense fallback={<Skeleton className="h-24 w-full" />}>
      <GoogleCallbackInner />
    </Suspense>
  );
}
