import type {
  AuthResponse,
  CheckInDto,
  CheckInResultDto,
  HabitDto,
  HabitStatsDto,
  PagedResult,
  ProblemDetails,
  TodayHabitDto,
  UserDto,
  WeekOverviewDto,
} from "./types";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export const SESSION_COOKIE = "ht_session";

/** Access token lives only in memory; the refresh token stays in an httpOnly cookie. */
let accessToken: string | null = null;

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

/** Non-httpOnly marker cookie that lets the Next.js proxy guard route without an API round-trip. */
export function setSessionMarker(on: boolean): void {
  if (typeof document === "undefined") return;
  document.cookie = on
    ? `${SESSION_COOKIE}=1; path=/; max-age=${30 * 24 * 3600}; samesite=lax`
    : `${SESSION_COOKIE}=; path=/; max-age=0; samesite=lax`;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `API error ${status}`);
    this.name = "ApiError";
  }

  fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  /** Skip the automatic refresh-and-retry on 401 (used by auth endpoints themselves). */
  skipAuthRetry?: boolean;
}

async function rawRequest(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = {};
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (accessToken) headers["Authorization"] = `Bearer ${accessToken}`;

  return fetch(`${API_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    credentials: "include",
  });
}

async function parseProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await rawRequest(path, options);

  if (response.status === 401 && !options.skipAuthRetry) {
    const refreshed = await tryRefresh();
    if (!refreshed) {
      throw new ApiError(401, null);
    }
    response = await rawRequest(path, options);
  }

  if (!response.ok) {
    throw new ApiError(response.status, await parseProblem(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

let refreshPromise: Promise<boolean> | null = null;

/** Single-flight refresh: concurrent 401s share one refresh call. */
export async function tryRefresh(): Promise<boolean> {
  refreshPromise ??= (async () => {
    try {
      const response = await fetch(`${API_URL}/api/v1/auth/refresh`, {
        method: "POST",
        headers: { "X-CSRF": "1" },
        credentials: "include",
      });
      if (!response.ok) {
        setAccessToken(null);
        return false;
      }
      const auth = (await response.json()) as AuthResponse;
      setAccessToken(auth.accessToken);
      setSessionMarker(true);
      lastRefreshedUser = auth.user;
      return true;
    } catch {
      setAccessToken(null);
      return false;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

let lastRefreshedUser: UserDto | null = null;

export function getLastRefreshedUser(): UserDto | null {
  return lastRefreshedUser;
}

// --- Auth ---

export const authApi = {
  async register(email: string, password: string, timeZone: string): Promise<AuthResponse> {
    const auth = await request<AuthResponse>("/api/v1/auth/register", {
      method: "POST",
      body: { email, password, timeZone },
      skipAuthRetry: true,
    });
    setAccessToken(auth.accessToken);
    setSessionMarker(true);
    return auth;
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const auth = await request<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      body: { email, password },
      skipAuthRetry: true,
    });
    setAccessToken(auth.accessToken);
    setSessionMarker(true);
    return auth;
  },

  async logout(): Promise<void> {
    try {
      await fetch(`${API_URL}/api/v1/auth/logout`, {
        method: "POST",
        headers: { "X-CSRF": "1" },
        credentials: "include",
      });
    } finally {
      setAccessToken(null);
      setSessionMarker(false);
    }
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<AuthResponse> {
    const auth = await request<AuthResponse>("/api/v1/auth/change-password", {
      method: "POST",
      body: { currentPassword, newPassword },
    });
    setAccessToken(auth.accessToken);
    return auth;
  },

  async deleteAccount(password: string): Promise<void> {
    await request<void>("/api/v1/auth/account", {
      method: "DELETE",
      body: { password },
    });
    setAccessToken(null);
    setSessionMarker(false);
  },

  me(): Promise<UserDto> {
    return request<UserDto>("/api/v1/me");
  },

  /** Whether this server has Google credentials configured. */
  async googleAvailable(): Promise<boolean> {
    try {
      const result = await request<{ available: boolean }>("/api/v1/auth/google/available", {
        skipAuthRetry: true,
      });
      return result.available;
    } catch {
      return false;
    }
  },

  /** Full-page navigation: the backend owns the OAuth redirect dance. */
  startGoogleSignIn(returnPath = "/"): void {
    window.location.href =
      `${API_URL}/api/v1/auth/google/start?returnPath=${encodeURIComponent(returnPath)}`;
  },

  updateTimeZone(timeZone: string): Promise<UserDto> {
    return request<UserDto>("/api/v1/me", { method: "PUT", body: { timeZone } });
  },
};

// --- Habits ---

export interface HabitPayload {
  name: string;
  description: string | null;
  color: string;
  icon: string;
  category: string | null;
  type?: string;
  targetValue: number | null;
  unit: string | null;
  scheduleType: string;
  scheduleDays: string[] | null;
  timesPerWeek: number | null;
}

export const habitsApi = {
  list(includeArchived = false): Promise<PagedResult<HabitDto>> {
    return request<PagedResult<HabitDto>>(
      `/api/v1/habits?includeArchived=${includeArchived}&pageSize=100`,
    );
  },

  get(id: string): Promise<HabitDto> {
    return request<HabitDto>(`/api/v1/habits/${id}`);
  },

  create(payload: HabitPayload): Promise<HabitDto> {
    return request<HabitDto>("/api/v1/habits", { method: "POST", body: payload });
  },

  update(id: string, payload: HabitPayload): Promise<HabitDto> {
    return request<HabitDto>(`/api/v1/habits/${id}`, { method: "PUT", body: payload });
  },

  archive(id: string): Promise<HabitDto> {
    return request<HabitDto>(`/api/v1/habits/${id}/archive`, { method: "POST" });
  },

  unarchive(id: string): Promise<HabitDto> {
    return request<HabitDto>(`/api/v1/habits/${id}/unarchive`, { method: "POST" });
  },

  reorder(habitIds: string[]): Promise<void> {
    return request<void>("/api/v1/habits/reorder", { method: "PUT", body: { habitIds } });
  },

  today(): Promise<TodayHabitDto[]> {
    return request<TodayHabitDto[]>("/api/v1/habits/today");
  },

  week(): Promise<WeekOverviewDto> {
    return request<WeekOverviewDto>("/api/v1/habits/week");
  },

  upsertCheckIn(
    habitId: string,
    date: string,
    value: number,
    increment = false,
  ): Promise<CheckInResultDto> {
    return request<CheckInResultDto>(`/api/v1/habits/${habitId}/checkins`, {
      method: "PUT",
      body: { date, value, increment },
    });
  },

  checkIns(habitId: string, from: string, to: string): Promise<CheckInDto[]> {
    return request<CheckInDto[]>(`/api/v1/habits/${habitId}/checkins?from=${from}&to=${to}`);
  },

  stats(habitId: string): Promise<HabitStatsDto> {
    return request<HabitStatsDto>(`/api/v1/habits/${habitId}/stats`);
  },
};
