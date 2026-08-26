import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ReminderSettings } from "./ReminderSettings";
import { tr } from "@/lib/i18n/tr";
import type { UserDto } from "@/lib/types";

const { updateRemindersMock, setUserMock } = vi.hoisted(() => ({
  updateRemindersMock: vi.fn(),
  setUserMock: vi.fn(),
}));

let currentUser: UserDto;

vi.mock("@/lib/api", () => ({
  authApi: { updateReminders: updateRemindersMock },
}));

vi.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: currentUser, setUser: setUserMock, status: "authenticated", logout: vi.fn() }),
}));

function makeUser(overrides: Partial<UserDto> = {}): UserDto {
  return {
    id: "u1",
    email: "kisi@example.com",
    timeZone: "Europe/Istanbul",
    createdAt: "2026-06-01T00:00:00Z",
    hasPassword: true,
    linkedGoogle: false,
    remindersEnabled: false,
    reminderHour: 20,
    isAdmin: false,
    ...overrides,
  };
}

describe("ReminderSettings", () => {
  it("starts from the user's saved preferences", () => {
    currentUser = makeUser({ remindersEnabled: true, reminderHour: 7 });
    render(<ReminderSettings />);

    expect(screen.getByLabelText(tr.settings.remindersEnabled)).toBeChecked();
    expect(screen.getByLabelText(tr.settings.reminderHour)).toHaveValue("7");
  });

  it("disables the hour picker while reminders are off", () => {
    currentUser = makeUser({ remindersEnabled: false });
    render(<ReminderSettings />);

    expect(screen.getByLabelText(tr.settings.reminderHour)).toBeDisabled();
  });

  it("enables reminders and saves the chosen hour", async () => {
    const user = userEvent.setup();
    currentUser = makeUser();
    updateRemindersMock.mockResolvedValue(makeUser({ remindersEnabled: true, reminderHour: 9 }));

    render(<ReminderSettings />);
    await user.click(screen.getByLabelText(tr.settings.remindersEnabled));
    await user.selectOptions(screen.getByLabelText(tr.settings.reminderHour), "9");
    await user.click(screen.getByRole("button", { name: tr.habits.save }));

    expect(updateRemindersMock).toHaveBeenCalledWith(true, 9);
    expect(await screen.findByRole("status")).toHaveTextContent(tr.settings.remindersSaved);
  });

  it("shows an error when saving fails", async () => {
    const user = userEvent.setup();
    currentUser = makeUser();
    updateRemindersMock.mockImplementation(() => {
      throw new Error("network");
    });

    render(<ReminderSettings />);
    await user.click(screen.getByRole("button", { name: tr.habits.save }));

    expect(await screen.findByRole("alert")).toHaveTextContent(tr.settings.saveError);
  });
});
