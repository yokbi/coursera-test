export type HabitType = "boolean" | "quantity" | "duration";
export type ScheduleType = "daily" | "specificWeekdays" | "timesPerWeek";
export type WeekdayName =
  | "monday"
  | "tuesday"
  | "wednesday"
  | "thursday"
  | "friday"
  | "saturday"
  | "sunday";

export const WEEKDAY_ORDER: WeekdayName[] = [
  "monday",
  "tuesday",
  "wednesday",
  "thursday",
  "friday",
  "saturday",
  "sunday",
];

export interface UserDto {
  id: string;
  email: string;
  timeZone: string;
  createdAt: string;
  /** False for accounts created through Google that never set a password. */
  hasPassword: boolean;
  linkedGoogle: boolean;
  remindersEnabled: boolean;
  /** Hour of day (0-23) in the user's own timezone. */
  reminderHour: number;
  isAdmin: boolean;
  shareStreaksWithFriends: boolean;
}

export interface FriendDto {
  userId: string;
  email: string;
  sharingEnabled: boolean;
  /** All stats are null unless that friend turned sharing on. */
  activeHabits: number | null;
  bestStreak: number | null;
  bestStreakUnit: "days" | "weeks" | null;
  bestStreakHabitName: string | null;
  completedToday: number | null;
  scheduledToday: number | null;
  friendsSince: string;
}

export interface FriendRequestDto {
  requestId: string;
  userId: string;
  email: string;
  createdAt: string;
}

export interface GroupSummaryDto {
  id: string;
  name: string;
  description: string | null;
  color: string;
  icon: string;
  isOwner: boolean;
  joined: boolean;
  memberCount: number;
  createdAt: string;
}

export interface GroupMemberProgressDto {
  userId: string;
  email: string;
  isOwner: boolean;
  isYou: boolean;
  habitName: string;
  habitIcon: string;
  currentStreak: number;
  streakUnit: "days" | "weeks";
  completedToday: boolean;
  scheduledToday: boolean;
  completionsLast7Days: number;
  joinedAt: string;
}

export interface GroupDetailDto {
  id: string;
  name: string;
  description: string | null;
  color: string;
  icon: string;
  isOwner: boolean;
  members: GroupMemberProgressDto[];
  pendingInvitees: { userId: string; email: string }[];
  createdAt: string;
}

export interface FriendRequestsDto {
  incoming: FriendRequestDto[];
  outgoing: FriendRequestDto[];
}

export interface AdminUserDto {
  id: string;
  email: string;
  role: "user" | "admin";
  timeZone: string;
  isSuspended: boolean;
  suspendedAt: string | null;
  isDeleted: boolean;
  remindersEnabled: boolean;
  habitCount: number;
  createdAt: string;
}

export interface AdminMetricsDto {
  totalUsers: number;
  activeUsers: number;
  suspendedUsers: number;
  deletedUsers: number;
  usersWithRemindersOn: number;
  totalHabits: number;
  archivedHabits: number;
  checkInsLast7Days: number;
  newUsersLast30Days: number;
}

export interface AuthResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: UserDto;
}

export interface HabitDto {
  id: string;
  name: string;
  description: string | null;
  color: string;
  icon: string;
  category: string | null;
  type: HabitType;
  targetValue: number | null;
  unit: string | null;
  scheduleType: ScheduleType;
  scheduleDays: WeekdayName[];
  timesPerWeek: number | null;
  sortOrder: number;
  isArchived: boolean;
  startDate: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TodayHabitDto {
  habit: HabitDto;
  todayValue: number;
  completedToday: boolean;
  currentStreak: number;
  streakUnit: "days" | "weeks";
  weekCompletions: number;
}

export interface CheckInDto {
  date: string;
  value: number;
  completed: boolean;
}

export interface CheckInResultDto {
  checkIn: CheckInDto;
  currentStreak: number;
  streakUnit: "days" | "weeks";
}

export interface WeekDayCellDto {
  date: string;
  value: number;
  completed: boolean;
  scheduled: boolean;
}

export interface WeekHabitRowDto {
  habit: HabitDto;
  days: WeekDayCellDto[];
}

export interface WeekOverviewDto {
  weekStart: string;
  habits: WeekHabitRowDto[];
}

export interface HeatmapDayDto {
  date: string;
  value: number;
  completed: boolean;
  scheduled: boolean;
}

export interface HabitStatsDto {
  habitId: string;
  currentStreak: number;
  longestStreak: number;
  streakUnit: "days" | "weeks";
  completionRate30: number;
  completionRate90: number;
  totalCheckIns: number;
  heatmap90: HeatmapDayDto[];
}

export interface HabitFormValues {
  name: string;
  description: string;
  color: string;
  icon: string;
  category: string;
  type: HabitType;
  targetValue: number | null;
  unit: string;
  scheduleType: ScheduleType;
  scheduleDays: WeekdayName[];
  timesPerWeek: number | null;
}

export interface ProblemDetails {
  status?: number;
  title?: string;
  errors?: Record<string, string[]>;
}
