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
