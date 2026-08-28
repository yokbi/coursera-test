import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import GroupsPage from "./gruplar/page";
import { GroupDetailView } from "./gruplar/[id]/page";
import { tr } from "@/lib/i18n/tr";
import type { GroupDetailDto, GroupSummaryDto, HabitDto } from "@/lib/types";

const {
  listMock, getMock, createMock, joinMock, declineMock, inviteMock,
  removeMemberMock, leaveMock, removeMock, habitsListMock, friendsListMock, replaceMock,
} = vi.hoisted(() => ({
  listMock: vi.fn(), getMock: vi.fn(), createMock: vi.fn(), joinMock: vi.fn(),
  declineMock: vi.fn(), inviteMock: vi.fn(), removeMemberMock: vi.fn(),
  leaveMock: vi.fn(), removeMock: vi.fn(), habitsListMock: vi.fn(),
  friendsListMock: vi.fn(), replaceMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  groupsApi: {
    list: listMock, get: getMock, create: createMock, join: joinMock,
    decline: declineMock, invite: inviteMock, removeMember: removeMemberMock,
    leave: leaveMock, remove: removeMock, changeHabit: vi.fn(),
  },
  habitsApi: { list: habitsListMock },
  friendsApi: { list: friendsListMock },
}));

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: replaceMock }) }));

function habit(id = "h1", name = "Koşu"): HabitDto {
  return {
    id, name, description: null, color: "#22c55e", icon: "🏃", category: null,
    type: "boolean", targetValue: null, unit: null, scheduleType: "daily",
    scheduleDays: [], timesPerWeek: null, sortOrder: 0, isArchived: false,
    startDate: "2026-06-01", createdAt: "2026-06-01T00:00:00Z",
  };
}

function summary(overrides: Partial<GroupSummaryDto> = {}): GroupSummaryDto {
  return {
    id: "g1", name: "Sabah koşusu", description: null, color: "#22c55e", icon: "🏃",
    isOwner: true, joined: true, memberCount: 2, createdAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

function detail(overrides: Partial<GroupDetailDto> = {}): GroupDetailDto {
  return {
    id: "g1", name: "Sabah koşusu", description: "Birlikte", color: "#22c55e", icon: "🏃",
    isOwner: true,
    members: [
      {
        userId: "u1", email: "ben@example.com", isOwner: true, isYou: true,
        habitName: "Koşu", habitIcon: "🏃", currentStreak: 5, streakUnit: "days",
        completedToday: true, scheduledToday: true, completionsLast7Days: 5,
        joinedAt: "2026-07-01T00:00:00Z",
      },
      {
        userId: "u2", email: "arkadas@example.com", isOwner: false, isYou: false,
        habitName: "Sabah koşusu", habitIcon: "🏃", currentStreak: 2, streakUnit: "days",
        completedToday: false, scheduledToday: true, completionsLast7Days: 3,
        joinedAt: "2026-07-02T00:00:00Z",
      },
    ],
    pendingInvitees: [],
    createdAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

describe("GroupsPage", () => {
  it("shows the empty state when there are no groups", async () => {
    listMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    render(<GroupsPage />);

    expect(await screen.findByText(tr.groups.empty)).toBeInTheDocument();
  });

  it("requires a name and a linked habit before creating", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    render(<GroupsPage />);
    await user.click(await screen.findByRole("button", { name: tr.groups.newGroup }));
    await user.click(screen.getByRole("button", { name: tr.groups.create }));

    expect(await screen.findByText(tr.groups.nameRequired)).toBeInTheDocument();
    expect(screen.getByText(tr.groups.habitRequired)).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("creates a group with the selected habit", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });
    createMock.mockResolvedValue(summary());

    render(<GroupsPage />);
    await user.click(await screen.findByRole("button", { name: tr.groups.newGroup }));
    await user.type(screen.getByLabelText(tr.groups.name), "Sabah koşusu");
    await user.selectOptions(screen.getByLabelText(tr.groups.linkedHabit), "h1");
    await user.click(screen.getByRole("button", { name: tr.groups.create }));

    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({ name: "Sabah koşusu", habitId: "h1" }),
    );
  });

  it("lists a pending invitation separately and can decline it", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([summary({ joined: false, isOwner: false })]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });
    declineMock.mockResolvedValue(undefined);

    render(<GroupsPage />);

    expect(await screen.findByText(tr.groups.invitations)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: tr.groups.decline }));
    expect(declineMock).toHaveBeenCalledWith("g1");
  });

  it("joins an invitation with a chosen habit", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([summary({ joined: false, isOwner: false })]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });
    joinMock.mockResolvedValue(undefined);

    render(<GroupsPage />);
    await user.selectOptions(await screen.findByLabelText(tr.groups.linkedHabit), "h1");

    expect(joinMock).toHaveBeenCalledWith("g1", "h1");
  });
});

describe("GroupDetailPage", () => {
  const renderDetail = () => render(<GroupDetailView groupId="g1" />);

  it("shows each member's group-scoped progress", async () => {
    getMock.mockResolvedValue(detail());
    friendsListMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    renderDetail();

    expect(await screen.findByText(/ben@example.com/)).toBeInTheDocument();
    expect(screen.getByText(/arkadas@example.com/)).toBeInTheDocument();
    expect(screen.getByText(new RegExp(tr.groups.doneToday))).toBeInTheDocument();
    expect(screen.getByText(new RegExp(tr.groups.notDoneToday))).toBeInTheDocument();
  });

  it("lets the owner remove another member but not themselves", async () => {
    getMock.mockResolvedValue(detail());
    friendsListMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    renderDetail();
    await screen.findByText(/arkadas@example.com/);

    // Only one remove control: the owner's own row has none.
    expect(screen.getAllByRole("button", { name: tr.groups.removeMember })).toHaveLength(1);
  });

  it("offers delete to the owner and leave to a member", async () => {
    getMock.mockResolvedValue(detail({ isOwner: false }));
    friendsListMock.mockResolvedValue([]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    renderDetail();

    expect(await screen.findByRole("button", { name: tr.groups.leave })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: tr.groups.deleteGroup })).not.toBeInTheDocument();
  });

  it("hides the invite panel from non-owners", async () => {
    getMock.mockResolvedValue(detail({ isOwner: false }));
    friendsListMock.mockResolvedValue([
      {
        userId: "f1", email: "baska@example.com", sharingEnabled: false, activeHabits: null,
        bestStreak: null, bestStreakUnit: null, bestStreakHabitName: null,
        completedToday: null, scheduledToday: null, friendsSince: "2026-06-01T00:00:00Z",
      },
    ]);
    habitsListMock.mockResolvedValue({ items: [habit()], page: 1, pageSize: 100, totalCount: 1 });

    renderDetail();
    await screen.findByText(/arkadas@example.com/);

    expect(screen.queryByText(tr.groups.invite)).not.toBeInTheDocument();
  });

  it("shows an error state when loading fails", async () => {
    getMock.mockImplementation(() => Promise.reject(new Error("boom")));
    friendsListMock.mockImplementation(() => Promise.reject(new Error("boom")));
    habitsListMock.mockImplementation(() => Promise.reject(new Error("boom")));

    renderDetail();

    expect(await screen.findByText(tr.groups.loadError)).toBeInTheDocument();
  });
});
