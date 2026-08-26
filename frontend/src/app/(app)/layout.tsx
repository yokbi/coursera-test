"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { AuthProvider, useAuth } from "@/lib/auth-context";
import { tr } from "@/lib/i18n/tr";
import { ThemeToggle } from "@/components/ThemeToggle";
import { Skeleton } from "@/components/ui";

function NavLink({ href, label }: { href: string; label: string }) {
  const pathname = usePathname();
  const active = href === "/" ? pathname === "/" : pathname.startsWith(href);
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={`rounded-lg px-3 py-2 text-sm font-medium focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 ${
        active
          ? "bg-sky-100 text-sky-800 dark:bg-sky-950 dark:text-sky-300"
          : "hover:bg-slate-100 dark:hover:bg-slate-800"
      }`}
    >
      {label}
    </Link>
  );
}

function AppShell({ children }: { children: React.ReactNode }) {
  const { status, logout, user } = useAuth();

  if (status === "loading") {
    return (
      <div className="mx-auto w-full max-w-4xl px-4 py-8 flex flex-col gap-4">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (status !== "authenticated") {
    return null; // AuthProvider is redirecting to /giris.
  }

  return (
    <>
      <header className="border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
        <div className="mx-auto flex w-full max-w-4xl items-center justify-between gap-2 px-4 py-3">
          <Link href="/" className="text-base font-bold">
            {tr.app.name}
          </Link>
          <nav aria-label={tr.app.name} className="flex items-center gap-1">
            <NavLink href="/" label={tr.nav.dashboard} />
            <NavLink href="/aliskanliklar" label={tr.nav.habits} />
            <NavLink href="/ayarlar" label={tr.nav.settings} />
            {user?.isAdmin ? <NavLink href="/yonetim" label={tr.nav.admin} /> : null}
            <ThemeToggle />
            <button
              type="button"
              onClick={() => void logout()}
              className="rounded-lg px-3 py-2 text-sm font-medium hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500"
            >
              {tr.nav.logout}
            </button>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-6">{children}</main>
    </>
  );
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthProvider>
      <AppShell>{children}</AppShell>
    </AuthProvider>
  );
}
