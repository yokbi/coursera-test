import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AdminPage from "./yonetim/page";
import { tr } from "@/lib/i18n/tr";
import type { AdminMetricsDto, AdminUserDto, UserDto } from "@/lib/types";

const { metricsMock, listUsersMock, suspendMock, unsuspendMock, replaceMock } = vi.hoisted(() => ({
  metricsMock: vi.fn(),
  listUsersMock: vi.fn(),
  suspendMock: vi.fn(),
  unsuspendMock: vi.fn(),
  replaceMock: vi.fn(),
}));

let currentUser: UserDto;

vi.mock("@/lib/api", () => ({
  adminApi: {
    metrics: metricsMock,
    listUsers: listUsersMock,
    suspend: suspendMock,
    unsuspend: unsuspendMock,
  },
}));

vi.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: currentUser, setUser: vi.fn(), status: "authenticated", logout: vi.fn() }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
}));

function admin(isAdmin = true): UserDto {
  return {
    id: "u1",
    email: "admin@example.com",
    timeZone: "Europe/Istanbul",
    createdAt: "2026-06-01T00:00:00Z",
    hasPassword: true,
    linkedGoogle: false,
    remindersEnabled: false,
    reminderHour: 20,
    isAdmin,
  };
}

const metrics: AdminMetricsDto = {
  totalUsers: 12,
  activeUsers: 10,
  suspendedUsers: 1,
  deletedUsers: 1,
  usersWithRemindersOn: 4,
  totalHabits: 30,
  archivedHabits: 3,
  checkInsLast7Days: 88,
  newUsersLast30Days: 5,
};

function row(overrides: Partial<AdminUserDto> = {}): AdminUserDto {
  return {
    id: "target-1",
    email: "kisi@example.com",
    role: "user",
    timeZone: "UTC",
    isSuspended: false,
    suspendedAt: null,
    isDeleted: false,
    remindersEnabled: false,
    habitCount: 3,
    createdAt: "2026-06-15T00:00:00Z",
    ...overrides,
  };
}

describe("AdminPage", () => {
  it("redirects a non-admin away and renders nothing", () => {
    currentUser = admin(false);
    const { container } = render(<AdminPage />);

    expect(replaceMock).toHaveBeenCalledWith("/");
    expect(container).toBeEmptyDOMElement();
  });

  it("shows metrics and the user table once loaded", async () => {
    currentUser = admin();
    metricsMock.mockResolvedValue(metrics);
    listUsersMock.mockResolvedValue({ items: [row()], page: 1, pageSize: 25, totalCount: 1 });

    render(<AdminPage />);

    expect(await screen.findByText(tr.admin.title)).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument();
    expect(screen.getByText("kisi@example.com")).toBeInTheDocument();
    expect(screen.getByText(tr.admin.statusActive)).toBeInTheDocument();
  });

  it("suspends a user and reflects the new status", async () => {
    const user = userEvent.setup();
    currentUser = admin();
    metricsMock.mockResolvedValue(metrics);
    listUsersMock.mockResolvedValue({ items: [row()], page: 1, pageSize: 25, totalCount: 1 });
    suspendMock.mockResolvedValue(row({ isSuspended: true, suspendedAt: "2026-07-09T00:00:00Z" }));

    render(<AdminPage />);
    await user.click(await screen.findByRole("button", { name: tr.admin.suspend }));

    expect(suspendMock).toHaveBeenCalledWith("target-1");
    expect(await screen.findByText(tr.admin.statusSuspended)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: tr.admin.unsuspend })).toBeInTheDocument();
  });

  it("offers no suspend control for admin accounts", async () => {
    currentUser = admin();
    metricsMock.mockResolvedValue(metrics);
    listUsersMock.mockResolvedValue({
      items: [row({ role: "admin", email: "other-admin@example.com" })],
      page: 1,
      pageSize: 25,
      totalCount: 1,
    });

    render(<AdminPage />);

    expect(await screen.findByText("other-admin@example.com")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: tr.admin.suspend })).not.toBeInTheDocument();
  });

  it("shows an error state when loading fails", async () => {
    currentUser = admin();
    metricsMock.mockImplementation(() => Promise.reject(new Error("boom")));
    listUsersMock.mockImplementation(() => Promise.reject(new Error("boom")));

    render(<AdminPage />);

    expect(await screen.findByText(tr.admin.loadError)).toBeInTheDocument();
  });

  it("searches by email", async () => {
    const user = userEvent.setup();
    currentUser = admin();
    metricsMock.mockResolvedValue(metrics);
    listUsersMock.mockResolvedValue({ items: [row()], page: 1, pageSize: 25, totalCount: 1 });

    render(<AdminPage />);
    await screen.findByText(tr.admin.title);

    await user.type(screen.getByLabelText(tr.admin.search), "kisi@");
    await user.click(screen.getByRole("button", { name: tr.admin.search }));

    expect(listUsersMock).toHaveBeenLastCalledWith("kisi@", 1, 25);
  });
});
