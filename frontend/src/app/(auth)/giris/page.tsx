"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError, authApi } from "@/lib/api";
import { fieldErrors, loginSchema } from "@/lib/habit-schema";
import { tr } from "@/lib/i18n/tr";
import { Field, FormError, inputClass, primaryButtonClass } from "@/components/ui";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setServerError(null);

    const parsed = loginSchema.safeParse({ email, password });
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setSubmitting(true);
    try {
      await authApi.login(parsed.data.email, parsed.data.password);
      router.replace("/");
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setServerError(tr.auth.rateLimited);
      } else {
        setServerError(tr.auth.genericError);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold">{tr.auth.loginTitle}</h2>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <FormError message={serverError} />
        <Field label={tr.auth.email} htmlFor="email" error={errors.email}>
          <input
            id="email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label={tr.auth.password} htmlFor="password" error={errors.password}>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className={inputClass}
          />
        </Field>
        <button type="submit" disabled={submitting} className={primaryButtonClass}>
          {submitting ? tr.common.loading : tr.auth.loginButton}
        </button>
      </form>
      <p className="text-sm text-slate-600 dark:text-slate-400">
        {tr.auth.noAccount}{" "}
        <Link href="/kayit" className="font-medium text-sky-600 hover:underline">
          {tr.auth.registerLink}
        </Link>
      </p>
    </div>
  );
}
