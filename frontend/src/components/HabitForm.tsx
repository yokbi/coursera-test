"use client";

import { useState } from "react";
import { ApiError, habitsApi, type HabitPayload } from "@/lib/api";
import { fieldErrors, habitSchema } from "@/lib/habit-schema";
import { tr } from "@/lib/i18n/tr";
import { WEEKDAY_ORDER, type HabitDto, type HabitFormValues } from "@/lib/types";
import {
  Field,
  FormError,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
} from "@/components/ui";

const ICON_CHOICES = ["✅", "🧘", "💧", "📚", "🏃", "🏊", "💪", "🎯", "🌙", "🥗", "✍️", "🎸"] as const;
const COLOR_CHOICES = [
  "#22c55e",
  "#0ea5e9",
  "#8b5cf6",
  "#f59e0b",
  "#ef4444",
  "#ec4899",
  "#06b6d4",
  "#64748b",
] as const;

function emptyValues(): HabitFormValues {
  return {
    name: "",
    description: "",
    color: COLOR_CHOICES[0],
    icon: ICON_CHOICES[0],
    category: "",
    type: "boolean",
    targetValue: null,
    unit: "",
    scheduleType: "daily",
    scheduleDays: [],
    timesPerWeek: null,
  };
}

function valuesFromHabit(habit: HabitDto): HabitFormValues {
  return {
    name: habit.name,
    description: habit.description ?? "",
    color: habit.color,
    icon: habit.icon,
    category: habit.category ?? "",
    type: habit.type,
    targetValue: habit.targetValue,
    unit: habit.unit ?? "",
    scheduleType: habit.scheduleType,
    scheduleDays: habit.scheduleDays,
    timesPerWeek: habit.timesPerWeek,
  };
}

export function HabitForm({
  habit,
  onSaved,
  onCancel,
}: {
  /** When provided the form edits this habit; otherwise it creates a new one. */
  habit?: HabitDto;
  onSaved: (saved: HabitDto) => void;
  onCancel: () => void;
}) {
  const [values, setValues] = useState<HabitFormValues>(
    habit ? valuesFromHabit(habit) : emptyValues(),
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const isEdit = habit !== undefined;

  function set<K extends keyof HabitFormValues>(key: K, value: HabitFormValues[K]) {
    setValues((previous) => ({ ...previous, [key]: value }));
  }

  function toggleDay(day: (typeof WEEKDAY_ORDER)[number]) {
    set(
      "scheduleDays",
      values.scheduleDays.includes(day)
        ? values.scheduleDays.filter((d) => d !== day)
        : [...values.scheduleDays, day],
    );
  }

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setServerError(null);

    const parsed = habitSchema.safeParse(values);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setSubmitting(true);

    const payload: HabitPayload = {
      name: parsed.data.name,
      description: parsed.data.description || null,
      color: parsed.data.color,
      icon: parsed.data.icon,
      category: parsed.data.category || null,
      targetValue: parsed.data.type === "boolean" ? null : parsed.data.targetValue,
      unit: parsed.data.type === "quantity" ? parsed.data.unit : null,
      scheduleType: parsed.data.scheduleType,
      scheduleDays:
        parsed.data.scheduleType === "specificWeekdays" ? parsed.data.scheduleDays : null,
      timesPerWeek:
        parsed.data.scheduleType === "timesPerWeek" ? parsed.data.timesPerWeek : null,
    };

    try {
      const saved = isEdit
        ? await habitsApi.update(habit.id, payload)
        : await habitsApi.create({ ...payload, type: parsed.data.type });
      onSaved(saved);
    } catch (error) {
      if (error instanceof ApiError && error.problem?.errors) {
        const backendErrors: Record<string, string> = {};
        for (const [key, messages] of Object.entries(error.problem.errors)) {
          backendErrors[key] = messages[0] ?? tr.habits.saveError;
        }
        setErrors(backendErrors);
      } else {
        setServerError(tr.habits.saveError);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={onSubmit}
      noValidate
      aria-label={isEdit ? tr.habits.editHabit : tr.habits.newHabit}
      className="flex flex-col gap-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4"
    >
      <h3 className="text-lg font-semibold">
        {isEdit ? tr.habits.editHabit : tr.habits.newHabit}
      </h3>
      <FormError message={serverError} />

      <Field label={tr.habits.name} htmlFor="habit-name" error={errors.name}>
        <input
          id="habit-name"
          value={values.name}
          maxLength={100}
          onChange={(e) => set("name", e.target.value)}
          className={inputClass}
        />
      </Field>

      <Field label={tr.habits.description} htmlFor="habit-description" error={errors.description}>
        <textarea
          id="habit-description"
          value={values.description}
          maxLength={500}
          rows={2}
          onChange={(e) => set("description", e.target.value)}
          className={inputClass}
        />
      </Field>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={tr.habits.icon} htmlFor="habit-icon" error={errors.icon}>
          <div id="habit-icon" role="radiogroup" aria-label={tr.habits.icon} className="flex flex-wrap gap-1">
            {ICON_CHOICES.map((icon) => (
              <button
                key={icon}
                type="button"
                role="radio"
                aria-checked={values.icon === icon}
                aria-label={icon}
                onClick={() => set("icon", icon)}
                className={`h-9 w-9 rounded-lg border text-lg focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 ${
                  values.icon === icon
                    ? "border-sky-500 bg-sky-50 dark:bg-sky-950"
                    : "border-slate-300 dark:border-slate-700"
                }`}
              >
                {icon}
              </button>
            ))}
          </div>
        </Field>

        <Field label={tr.habits.color} htmlFor="habit-color" error={errors.color}>
          <div id="habit-color" role="radiogroup" aria-label={tr.habits.color} className="flex flex-wrap gap-1">
            {COLOR_CHOICES.map((color) => (
              <button
                key={color}
                type="button"
                role="radio"
                aria-checked={values.color === color}
                aria-label={color}
                onClick={() => set("color", color)}
                style={{ backgroundColor: color }}
                className={`h-9 w-9 rounded-lg border-2 focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 ${
                  values.color === color ? "border-slate-900 dark:border-white" : "border-transparent"
                }`}
              />
            ))}
          </div>
        </Field>
      </div>

      <Field label={tr.habits.category} htmlFor="habit-category" error={errors.category}>
        <input
          id="habit-category"
          value={values.category}
          maxLength={50}
          onChange={(e) => set("category", e.target.value)}
          className={inputClass}
        />
      </Field>

      <Field label={tr.habits.type} htmlFor="habit-type" error={errors.type}>
        <select
          id="habit-type"
          value={values.type}
          disabled={isEdit}
          aria-describedby={isEdit ? "type-locked-note" : undefined}
          onChange={(e) => set("type", e.target.value as HabitFormValues["type"])}
          className={inputClass}
        >
          <option value="boolean">{tr.habits.typeBoolean}</option>
          <option value="quantity">{tr.habits.typeQuantity}</option>
          <option value="duration">{tr.habits.typeDuration}</option>
        </select>
        {isEdit ? (
          <p id="type-locked-note" className="text-xs text-slate-500 dark:text-slate-400">
            {tr.habits.typeLocked}
          </p>
        ) : null}
      </Field>

      {values.type !== "boolean" ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={tr.habits.targetValue} htmlFor="habit-target" error={errors.targetValue}>
            <input
              id="habit-target"
              type="number"
              inputMode="numeric"
              min={1}
              max={100000}
              value={values.targetValue ?? ""}
              onChange={(e) =>
                set("targetValue", e.target.value === "" ? null : Number(e.target.value))
              }
              className={inputClass}
            />
          </Field>
          {values.type === "quantity" ? (
            <Field label={tr.habits.unit} htmlFor="habit-unit" error={errors.unit}>
              <input
                id="habit-unit"
                value={values.unit}
                maxLength={30}
                placeholder={tr.habits.unitPlaceholder}
                onChange={(e) => set("unit", e.target.value)}
                className={inputClass}
              />
            </Field>
          ) : null}
        </div>
      ) : null}

      <Field label={tr.habits.schedule} htmlFor="habit-schedule" error={errors.scheduleType}>
        <select
          id="habit-schedule"
          value={values.scheduleType}
          onChange={(e) => set("scheduleType", e.target.value as HabitFormValues["scheduleType"])}
          className={inputClass}
        >
          <option value="daily">{tr.habits.scheduleDaily}</option>
          <option value="specificWeekdays">{tr.habits.scheduleWeekdays}</option>
          <option value="timesPerWeek">{tr.habits.scheduleTimesPerWeek}</option>
        </select>
      </Field>

      {values.scheduleType === "specificWeekdays" ? (
        <Field label={tr.habits.scheduleWeekdays} htmlFor="habit-days" error={errors.scheduleDays}>
          <div id="habit-days" className="flex flex-wrap gap-1">
            {WEEKDAY_ORDER.map((day, index) => (
              <button
                key={day}
                type="button"
                aria-pressed={values.scheduleDays.includes(day)}
                onClick={() => toggleDay(day)}
                className={`rounded-lg border px-3 py-2 text-sm focus:outline-2 focus:outline-offset-2 focus:outline-sky-500 ${
                  values.scheduleDays.includes(day)
                    ? "border-sky-500 bg-sky-50 text-sky-800 dark:bg-sky-950 dark:text-sky-300"
                    : "border-slate-300 dark:border-slate-700"
                }`}
              >
                {tr.weekdaysShort[index]}
              </button>
            ))}
          </div>
        </Field>
      ) : null}

      {values.scheduleType === "timesPerWeek" ? (
        <Field label={tr.habits.timesPerWeek} htmlFor="habit-times" error={errors.timesPerWeek}>
          <input
            id="habit-times"
            type="number"
            inputMode="numeric"
            min={1}
            max={7}
            value={values.timesPerWeek ?? ""}
            onChange={(e) =>
              set("timesPerWeek", e.target.value === "" ? null : Number(e.target.value))
            }
            className={inputClass}
          />
        </Field>
      ) : null}

      <div className="flex gap-2">
        <button type="submit" disabled={submitting} className={primaryButtonClass}>
          {submitting ? tr.common.loading : isEdit ? tr.habits.save : tr.habits.create}
        </button>
        <button type="button" onClick={onCancel} className={secondaryButtonClass}>
          {tr.habits.cancel}
        </button>
      </div>
    </form>
  );
}
