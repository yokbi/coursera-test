"use client";

import { tr } from "@/lib/i18n/tr";
import type { HeatmapDayDto } from "@/lib/types";

/**
 * GitHub-style 90-day heatmap: columns are weeks, rows are Mon–Sun.
 * Cell intensity is flat (completed or not); unscheduled days are dimmed.
 */
export function Heatmap({ days, color }: { days: HeatmapDayDto[]; color: string }) {
  // Pad the front so the first column starts on Monday.
  const firstDay = new Date(`${days[0]?.date}T00:00:00`);
  const padCount = firstDay ? (firstDay.getDay() + 6) % 7 : 0;
  const cells: (HeatmapDayDto | null)[] = [...Array<null>(padCount).fill(null), ...days];
  const weeks: (HeatmapDayDto | null)[][] = [];
  for (let i = 0; i < cells.length; i += 7) {
    weeks.push(cells.slice(i, i + 7));
  }

  return (
    <div className="overflow-x-auto">
      <div className="flex gap-1" role="img" aria-label={tr.stats.heatmapTitle}>
        {weeks.map((week, weekIndex) => (
          <div key={weekIndex} className="flex flex-col gap-1">
            {week.map((cell, dayIndex) =>
              cell === null ? (
                <span key={dayIndex} className="h-4 w-4" aria-hidden="true" />
              ) : (
                <span
                  key={cell.date}
                  title={`${cell.date}: ${
                    cell.completed
                      ? tr.dashboard.completed
                      : cell.scheduled
                        ? tr.checkin.notDone
                        : tr.stats.notScheduled
                  }`}
                  data-completed={cell.completed}
                  className={`h-4 w-4 rounded-sm ${
                    cell.completed
                      ? ""
                      : cell.scheduled
                        ? "bg-slate-200 dark:bg-slate-800"
                        : "bg-slate-100 dark:bg-slate-900 opacity-60"
                  }`}
                  style={cell.completed ? { backgroundColor: color } : undefined}
                />
              ),
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
