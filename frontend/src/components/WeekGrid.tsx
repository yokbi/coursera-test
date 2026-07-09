"use client";

import { tr } from "@/lib/i18n/tr";
import type { WeekOverviewDto } from "@/lib/types";

/** Weekly overview: one row per habit, one cell per day (Mon–Sun). */
export function WeekGrid({ week }: { week: WeekOverviewDto }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-separate border-spacing-1 text-sm">
        <thead>
          <tr>
            <th scope="col" className="min-w-28 text-left font-medium text-slate-500 dark:text-slate-400">
              {tr.nav.habits}
            </th>
            {tr.weekdaysShort.map((day) => (
              <th
                key={day}
                scope="col"
                className="w-9 text-center font-medium text-slate-500 dark:text-slate-400"
              >
                {day}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {week.habits.map((row) => (
            <tr key={row.habit.id}>
              <th scope="row" className="max-w-40 truncate text-left font-normal">
                <span aria-hidden="true" className="mr-1">
                  {row.habit.icon}
                </span>
                {row.habit.name}
              </th>
              {row.days.map((cell) => (
                <td key={cell.date} className="text-center">
                  <span
                    role="img"
                    aria-label={`${row.habit.name} ${cell.date}: ${
                      cell.completed
                        ? tr.dashboard.completed
                        : cell.scheduled
                          ? tr.checkin.notDone
                          : tr.stats.notScheduled
                    }`}
                    data-completed={cell.completed}
                    className={`inline-block h-7 w-7 rounded-md border ${
                      cell.completed
                        ? "border-transparent"
                        : cell.scheduled
                          ? "border-slate-300 dark:border-slate-700"
                          : "border-slate-200 dark:border-slate-800 opacity-40"
                    }`}
                    style={cell.completed ? { backgroundColor: row.habit.color } : undefined}
                  />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
