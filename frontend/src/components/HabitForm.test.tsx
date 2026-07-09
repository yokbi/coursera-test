import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HabitForm } from "./HabitForm";
import { tr } from "@/lib/i18n/tr";
import type { HabitDto } from "@/lib/types";

const { createMock, updateMock } = vi.hoisted(() => ({
  createMock: vi.fn(),
  updateMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  ApiError: class ApiError extends Error {
    constructor(
      public status: number,
      public problem: unknown,
    ) {
      super("api error");
    }
  },
  habitsApi: {
    create: createMock,
    update: updateMock,
  },
}));

describe("HabitForm", () => {
  beforeEach(() => {
    createMock.mockReset();
    updateMock.mockReset();
  });

  it("shows a validation error when the name is empty and does not submit", async () => {
    const user = userEvent.setup();
    render(<HabitForm onSaved={vi.fn()} onCancel={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: tr.habits.create }));

    expect(await screen.findByText(tr.habits.nameRequired)).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("requires target value and unit for quantity habits", async () => {
    const user = userEvent.setup();
    render(<HabitForm onSaved={vi.fn()} onCancel={vi.fn()} />);

    await user.type(screen.getByLabelText(tr.habits.name), "Su iç");
    await user.selectOptions(screen.getByLabelText(tr.habits.type), "quantity");
    await user.click(screen.getByRole("button", { name: tr.habits.create }));

    expect(await screen.findByText(tr.habits.targetRequired)).toBeInTheDocument();
    expect(screen.getByText(tr.habits.unitRequired)).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("requires at least one weekday for specific-weekday schedules", async () => {
    const user = userEvent.setup();
    render(<HabitForm onSaved={vi.fn()} onCancel={vi.fn()} />);

    await user.type(screen.getByLabelText(tr.habits.name), "Koşu");
    await user.selectOptions(screen.getByLabelText(tr.habits.schedule), "specificWeekdays");
    await user.click(screen.getByRole("button", { name: tr.habits.create }));

    expect(await screen.findByText(tr.habits.daysRequired)).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("submits a valid boolean habit and calls onSaved", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const saved = { id: "h1" } as HabitDto;
    createMock.mockResolvedValue(saved);

    render(<HabitForm onSaved={onSaved} onCancel={vi.fn()} />);
    await user.type(screen.getByLabelText(tr.habits.name), "Meditasyon");
    await user.click(screen.getByRole("button", { name: tr.habits.create }));

    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({
        name: "Meditasyon",
        type: "boolean",
        scheduleType: "daily",
        targetValue: null,
        unit: null,
      }),
    );
    expect(onSaved).toHaveBeenCalledWith(saved);
  });

  it("submits selected weekdays for a specific-weekday habit", async () => {
    const user = userEvent.setup();
    createMock.mockResolvedValue({ id: "h2" } as HabitDto);

    render(<HabitForm onSaved={vi.fn()} onCancel={vi.fn()} />);
    await user.type(screen.getByLabelText(tr.habits.name), "Koşu");
    await user.selectOptions(screen.getByLabelText(tr.habits.schedule), "specificWeekdays");
    await user.click(screen.getByRole("button", { name: tr.weekdaysShort[0] }));
    await user.click(screen.getByRole("button", { name: tr.weekdaysShort[2] }));
    await user.click(screen.getByRole("button", { name: tr.habits.create }));

    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({
        scheduleType: "specificWeekdays",
        scheduleDays: ["monday", "wednesday"],
      }),
    );
  });

  it("edits an existing habit with the type selector disabled", async () => {
    const user = userEvent.setup();
    updateMock.mockResolvedValue({ id: "h3" } as HabitDto);
    const habit: HabitDto = {
      id: "h3",
      name: "Kitap oku",
      description: null,
      color: "#f59e0b",
      icon: "📚",
      category: null,
      type: "duration",
      targetValue: 30,
      unit: null,
      scheduleType: "daily",
      scheduleDays: [],
      timesPerWeek: null,
      sortOrder: 0,
      isArchived: false,
      startDate: "2026-06-01",
      createdAt: "2026-06-01T00:00:00Z",
    };

    render(<HabitForm habit={habit} onSaved={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.getByLabelText(tr.habits.type)).toBeDisabled();
    await user.clear(screen.getByLabelText(tr.habits.name));
    await user.type(screen.getByLabelText(tr.habits.name), "Roman oku");
    await user.click(screen.getByRole("button", { name: tr.habits.save }));

    expect(updateMock).toHaveBeenCalledWith(
      "h3",
      expect.objectContaining({ name: "Roman oku", targetValue: 30 }),
    );
  });
});
