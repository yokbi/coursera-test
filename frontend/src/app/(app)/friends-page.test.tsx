import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import FriendsPage from "./arkadaslar/page";
import { tr } from "@/lib/i18n/tr";
import type { FriendDto, FriendRequestsDto, UserDto } from "@/lib/types";

const { listMock, requestsMock, sendRequestMock, acceptMock, declineMock, removeMock, sharingMock, setUserMock } =
  vi.hoisted(() => ({
    listMock: vi.fn(),
    requestsMock: vi.fn(),
    sendRequestMock: vi.fn(),
    acceptMock: vi.fn(),
    declineMock: vi.fn(),
    removeMock: vi.fn(),
    sharingMock: vi.fn(),
    setUserMock: vi.fn(),
  }));

let currentUser: UserDto;

vi.mock("@/lib/api", () => ({
  friendsApi: {
    list: listMock,
    requests: requestsMock,
    sendRequest: sendRequestMock,
    accept: acceptMock,
    decline: declineMock,
    remove: removeMock,
    updateSharing: sharingMock,
  },
}));

vi.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: currentUser, setUser: setUserMock, status: "authenticated", logout: vi.fn() }),
}));

function makeUser(shareStreaksWithFriends = false): UserDto {
  return {
    id: "u1",
    email: "ben@example.com",
    timeZone: "Europe/Istanbul",
    createdAt: "2026-06-01T00:00:00Z",
    hasPassword: true,
    linkedGoogle: false,
    remindersEnabled: false,
    reminderHour: 20,
    isAdmin: false,
    shareStreaksWithFriends,
  };
}

function sharingFriend(): FriendDto {
  return {
    userId: "f1",
    email: "arkadas@example.com",
    sharingEnabled: true,
    activeHabits: 4,
    bestStreak: 12,
    bestStreakUnit: "days",
    bestStreakHabitName: "Meditasyon",
    completedToday: 2,
    scheduledToday: 3,
    friendsSince: "2026-06-10T00:00:00Z",
  };
}

function privateFriend(): FriendDto {
  return {
    userId: "f2",
    email: "sessiz@example.com",
    sharingEnabled: false,
    activeHabits: null,
    bestStreak: null,
    bestStreakUnit: null,
    bestStreakHabitName: null,
    completedToday: null,
    scheduledToday: null,
    friendsSince: "2026-06-11T00:00:00Z",
  };
}

const noRequests: FriendRequestsDto = { incoming: [], outgoing: [] };

describe("FriendsPage", () => {
  it("shows the empty state when there are no friends", async () => {
    currentUser = makeUser();
    listMock.mockResolvedValue([]);
    requestsMock.mockResolvedValue(noRequests);

    render(<FriendsPage />);

    expect(await screen.findByText(tr.friends.empty)).toBeInTheDocument();
  });

  it("shows a sharing friend's streak summary", async () => {
    currentUser = makeUser();
    listMock.mockResolvedValue([sharingFriend()]);
    requestsMock.mockResolvedValue(noRequests);

    render(<FriendsPage />);

    expect(await screen.findByText("arkadas@example.com")).toBeInTheDocument();
    expect(screen.getByText(/12 gün — Meditasyon/)).toBeInTheDocument();
    expect(screen.getByText(/Bugün 2\/3/)).toBeInTheDocument();
  });

  it("reveals nothing about a friend who has sharing off", async () => {
    currentUser = makeUser();
    listMock.mockResolvedValue([privateFriend()]);
    requestsMock.mockResolvedValue(noRequests);

    render(<FriendsPage />);

    expect(await screen.findByText("sessiz@example.com")).toBeInTheDocument();
    expect(screen.getByText(tr.friends.notSharing)).toBeInTheDocument();
    expect(screen.queryByText(/🔥/)).not.toBeInTheDocument();
  });

  it("rejects a malformed address before calling the API", async () => {
    const user = userEvent.setup();
    currentUser = makeUser();
    listMock.mockResolvedValue([]);
    requestsMock.mockResolvedValue(noRequests);

    render(<FriendsPage />);
    await screen.findByText(tr.friends.empty);

    await user.type(screen.getByLabelText(tr.friends.emailLabel), "not-an-email");
    await user.click(screen.getByRole("button", { name: tr.friends.send }));

    expect(await screen.findByText(tr.friends.emailInvalid)).toBeInTheDocument();
    expect(sendRequestMock).not.toHaveBeenCalled();
  });

  it("sends a request and shows the deliberately vague confirmation", async () => {
    const user = userEvent.setup();
    currentUser = makeUser();
    listMock.mockResolvedValue([]);
    requestsMock.mockResolvedValue(noRequests);
    sendRequestMock.mockResolvedValue(undefined);

    render(<FriendsPage />);
    await screen.findByText(tr.friends.empty);

    await user.type(screen.getByLabelText(tr.friends.emailLabel), "yeni@example.com");
    await user.click(screen.getByRole("button", { name: tr.friends.send }));

    expect(sendRequestMock).toHaveBeenCalledWith("yeni@example.com");
    expect(await screen.findByRole("status")).toHaveTextContent(tr.friends.sent);
  });

  it("accepts an incoming request", async () => {
    const user = userEvent.setup();
    currentUser = makeUser();
    listMock.mockResolvedValue([]);
    requestsMock.mockResolvedValue({
      incoming: [
        { requestId: "r1", userId: "f9", email: "isteyen@example.com", createdAt: "2026-07-01T00:00:00Z" },
      ],
      outgoing: [],
    });
    acceptMock.mockResolvedValue(undefined);

    render(<FriendsPage />);
    await user.click(await screen.findByRole("button", { name: tr.friends.accept }));

    expect(acceptMock).toHaveBeenCalledWith("r1");
  });

  it("toggles streak sharing", async () => {
    const user = userEvent.setup();
    currentUser = makeUser(false);
    listMock.mockResolvedValue([]);
    requestsMock.mockResolvedValue(noRequests);
    sharingMock.mockResolvedValue(makeUser(true));

    render(<FriendsPage />);
    await screen.findByText(tr.friends.empty);

    await user.click(screen.getByLabelText(tr.friends.sharingToggle));

    expect(sharingMock).toHaveBeenCalledWith(true);
    expect(setUserMock).toHaveBeenCalled();
  });

  it("shows an error state when loading fails", async () => {
    currentUser = makeUser();
    listMock.mockImplementation(() => Promise.reject(new Error("boom")));
    requestsMock.mockImplementation(() => Promise.reject(new Error("boom")));

    render(<FriendsPage />);

    expect(await screen.findByText(tr.friends.loadError)).toBeInTheDocument();
  });
});
