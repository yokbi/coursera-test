import { tr } from "@/lib/i18n/tr";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-8 px-4 py-12">
      <div className="text-center">
        <h1 className="text-2xl font-bold">{tr.app.name}</h1>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">{tr.app.tagline}</p>
      </div>
      <div className="w-full max-w-sm rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 shadow-sm">
        {children}
      </div>
    </main>
  );
}
