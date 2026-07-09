import { z } from "zod";
import { tr } from "./i18n/tr";

/** Mirrors the backend FluentValidation rules so users get instant feedback. */
export const habitSchema = z
  .object({
    name: z.string().trim().min(1, tr.habits.nameRequired).max(100, tr.habits.nameTooLong),
    description: z.string().trim().max(500, tr.habits.descriptionTooLong),
    color: z.string().regex(/^#[0-9a-fA-F]{6}$/, tr.habits.colorInvalid),
    icon: z.string().trim().min(1, tr.habits.iconRequired).max(16, tr.habits.iconRequired),
    category: z.string().trim().max(50, tr.habits.categoryTooLong),
    type: z.enum(["boolean", "quantity", "duration"]),
    targetValue: z.number().nullable(),
    unit: z.string().trim().max(30),
    scheduleType: z.enum(["daily", "specificWeekdays", "timesPerWeek"]),
    scheduleDays: z.array(
      z.enum(["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"]),
    ),
    timesPerWeek: z.number().nullable(),
  })
  .superRefine((values, ctx) => {
    if (values.type !== "boolean") {
      if (values.targetValue === null) {
        ctx.addIssue({ code: "custom", path: ["targetValue"], message: tr.habits.targetRequired });
      } else if (values.targetValue <= 0 || values.targetValue > 100000) {
        ctx.addIssue({ code: "custom", path: ["targetValue"], message: tr.habits.targetPositive });
      }
    }

    if (values.type === "quantity" && values.unit.length === 0) {
      ctx.addIssue({ code: "custom", path: ["unit"], message: tr.habits.unitRequired });
    }

    if (values.scheduleType === "specificWeekdays" && values.scheduleDays.length === 0) {
      ctx.addIssue({ code: "custom", path: ["scheduleDays"], message: tr.habits.daysRequired });
    }

    if (values.scheduleType === "timesPerWeek") {
      if (values.timesPerWeek === null || values.timesPerWeek < 1 || values.timesPerWeek > 7) {
        ctx.addIssue({
          code: "custom",
          path: ["timesPerWeek"],
          message: tr.habits.timesPerWeekRange,
        });
      }
    }
  });

export type HabitSchemaValues = z.infer<typeof habitSchema>;

export const loginSchema = z.object({
  email: z.string().trim().email(tr.auth.emailInvalid),
  password: z.string().min(1, tr.auth.passwordMin),
});

export const passwordSchema = z
  .string()
  .min(8, tr.auth.passwordMin)
  .max(128, tr.auth.passwordMin)
  .regex(/[a-z]/, tr.auth.passwordLower)
  .regex(/[A-Z]/, tr.auth.passwordUpper)
  .regex(/[0-9]/, tr.auth.passwordDigit);

export const registerSchema = z
  .object({
    email: z.string().trim().email(tr.auth.emailInvalid),
    password: passwordSchema,
    passwordAgain: z.string(),
  })
  .superRefine((values, ctx) => {
    if (values.password !== values.passwordAgain) {
      ctx.addIssue({ code: "custom", path: ["passwordAgain"], message: tr.auth.passwordMismatch });
    }
  });

/** First issue per field, keyed by field name. */
export function fieldErrors(error: z.ZodError): Record<string, string> {
  const result: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = String(issue.path[0] ?? "_");
    result[key] ??= issue.message;
  }
  return result;
}
