import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DashboardPage from "./page";
import { tr } from "@/lib/i18n/tr";
import type { TodayHabitDto, WeekOverviewDto } from "@/lib/types";

const { todayMock, weekMock } = vi.hoisted(() => ({
  todayMock: vi.fn(),
  weekMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  ApiError: class ApiError extends Error {},
  habitsApi: { today: todayMock, week: weekMock, upsertCheckIn: vi.fn() },
}));

const emptyWeek: WeekOverviewDto = { weekStart: "2026-07-06", habits: [] };

function todayEntry(): TodayHabitDto {
  return {
    habit: {
      id: "h1",
      name: "Meditasyon",
      description: null,
      color: "#8b5cf6",
      icon: "🧘",
      category: null,
      type: "boolean",
      targetValue: null,
      unit: null,
      scheduleType: "daily",
      scheduleDays: [],
      timesPerWeek: null,
      sortOrder: 0,
      isArchived: false,
      startDate: "2026-06-01",
      createdAt: "2026-06-01T00:00:00Z",
    },
    todayValue: 0,
    completedToday: false,
    currentStreak: 0,
    streakUnit: "days",
    weekCompletions: 0,
  };
}

describe("DashboardPage", () => {
  beforeEach(() => {
    todayMock.mockReset();
    weekMock.mockReset();
  });

  it("shows loading skeletons while data is being fetched", () => {
    todayMock.mockReturnValue(new Promise(() => undefined));
    weekMock.mockReturnValue(new Promise(() => undefined));

    render(<DashboardPage />);
    expect(screen.getByLabelText(tr.common.loading)).toBeInTheDocument();
  });

  it("shows the empty state with a CTA when there are no habits", async () => {
    todayMock.mockResolvedValue([]);
    weekMock.mockResolvedValue(emptyWeek);

    render(<DashboardPage />);
    expect(await screen.findByText(tr.dashboard.empty)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: tr.dashboard.emptyCta })).toBeInTheDocument();
  });

  it("shows an error state with retry when loading fails, then recovers", async () => {
    const user = userEvent.setup();
    todayMock.mockRejectedValueOnce(new Error("network"));
    weekMock.mockRejectedValueOnce(new Error("network"));

    render(<DashboardPage />);
    expect(await screen.findByText(tr.dashboard.error)).toBeInTheDocument();

    todayMock.mockResolvedValue([todayEntry()]);
    weekMock.mockResolvedValue(emptyWeek);
    await user.click(screen.getByRole("button", { name: tr.dashboard.retry }));

    expect(await screen.findByText("Meditasyon")).toBeInTheDocument();
  });

  it("renders today's habits when data loads", async () => {
    todayMock.mockResolvedValue([todayEntry()]);
    weekMock.mockResolvedValue({
      weekStart: "2026-07-06",
      habits: [
        {
          habit: todayEntry().habit,
          days: Array.from({ length: 7 }, (_, i) => ({
            date: `2026-07-${String(6 + i).padStart(2, "0")}`,
            value: 0,
            completed: false,
            scheduled: true,
          })),
        },
      ],
    });

    render(<DashboardPage />);
    expect(await screen.findByText(tr.dashboard.todayTitle)).toBeInTheDocument();
    // "Meditasyon" renders both in the today card and the week grid row.
    expect((await screen.findAllByText("Meditasyon")).length).toBeGreaterThanOrEqual(1);
    expect(await screen.findByText(tr.dashboard.weekTitle)).toBeInTheDocument();
  });
});
