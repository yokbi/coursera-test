import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HabitCard } from "./HabitCard";
import { tr } from "@/lib/i18n/tr";
import type { TodayHabitDto } from "@/lib/types";

const { upsertMock } = vi.hoisted(() => ({ upsertMock: vi.fn() }));

vi.mock("@/lib/api", () => ({
  ApiError: class ApiError extends Error {
    constructor(
      public status: number,
      public problem: unknown,
    ) {
      super("api error");
    }
  },
  habitsApi: { upsertCheckIn: upsertMock },
}));

function entry(overrides: Partial<TodayHabitDto> = {}): TodayHabitDto {
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
    currentStreak: 2,
    streakUnit: "days",
    weekCompletions: 0,
    ...overrides,
  };
}

// No beforeEach mock reset here: vitest 4 misattributes handled rejections from
// reset mocks as unhandled errors. Each test installs its own implementation,
// and assertions match against specific calls, so isolation is preserved.
describe("HabitCard", () => {
  it("toggles a boolean habit on and reflects the new streak", async () => {
    const user = userEvent.setup();
    upsertMock.mockResolvedValue({
      checkIn: { date: "2026-07-09", value: 1, completed: true },
      currentStreak: 3,
      streakUnit: "days",
    });

    render(<ul><HabitCard entry={entry()} /></ul>);
    const toggle = screen.getByRole("button", { pressed: false });
    await user.click(toggle);

    expect(upsertMock).toHaveBeenCalledWith("h1", expect.any(String), 1, false);
    expect(await screen.findByRole("button", { pressed: true })).toBeInTheDocument();
    expect(screen.getByText(`🔥 3 ${tr.dashboard.days} ${tr.dashboard.streak}`)).toBeInTheDocument();
  });

  it("toggles a completed boolean habit off with value 0", async () => {
    const user = userEvent.setup();
    upsertMock.mockResolvedValue({
      checkIn: { date: "2026-07-09", value: 0, completed: false },
      currentStreak: 0,
      streakUnit: "days",
    });

    render(
      <ul>
        <HabitCard entry={entry({ completedToday: true, todayValue: 1 })} />
      </ul>,
    );
    await user.click(screen.getByRole("button", { pressed: true }));

    expect(upsertMock).toHaveBeenCalledWith("h1", expect.any(String), 0, false);
  });

  it("increments a quantity habit and shows progress toward the target", async () => {
    const user = userEvent.setup();
    upsertMock.mockResolvedValue({
      checkIn: { date: "2026-07-09", value: 3, completed: false },
      currentStreak: 0,
      streakUnit: "days",
    });

    const quantityEntry = entry({
      habit: {
        ...entry().habit,
        type: "quantity",
        targetValue: 8,
        unit: "bardak",
        name: "Su iç",
      },
      todayValue: 2,
    });

    render(<ul><HabitCard entry={quantityEntry} /></ul>);
    await user.click(screen.getByRole("button", { name: `Su iç: ${tr.checkin.increment}` }));

    expect(upsertMock).toHaveBeenCalledWith("h1", expect.any(String), 1, true);
    expect(await screen.findByText("3/8 bardak")).toBeInTheDocument();
  });

  it("uses 5-minute steps for duration habits", async () => {
    const user = userEvent.setup();
    upsertMock.mockResolvedValue({
      checkIn: { date: "2026-07-09", value: 5, completed: false },
      currentStreak: 0,
      streakUnit: "days",
    });

    const durationEntry = entry({
      habit: { ...entry().habit, type: "duration", targetValue: 30, name: "Kitap oku" },
    });

    render(<ul><HabitCard entry={durationEntry} /></ul>);
    await user.click(
      screen.getByRole("button", { name: `Kitap oku: ${tr.checkin.increment}` }),
    );

    expect(upsertMock).toHaveBeenCalledWith("h1", expect.any(String), 5, true);
  });

  it("shows the edit-window message when the API rejects with 422", async () => {
    const user = userEvent.setup();
    const { ApiError } = await import("@/lib/api");
    // Synchronous throw instead of a rejected promise: vitest 4's mock result
    // tracking misreports rejected promises as unhandled after a beforeEach reset.
    upsertMock.mockImplementation(() => {
      throw new ApiError(422, null);
    });

    render(<ul><HabitCard entry={entry()} /></ul>);
    await user.click(screen.getByRole("button", { pressed: false }));

    expect(await screen.findByRole("alert")).toHaveTextContent(tr.checkin.editWindowError);
  });
});
