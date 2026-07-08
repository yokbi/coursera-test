using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Application.Habits;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.CheckIns;

public interface IDashboardService
{
    Task<IReadOnlyList<TodayHabitDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);
    Task<WeekOverviewDto> GetWeekAsync(Guid userId, DateOnly? weekStart, CancellationToken ct = default);
}

public class DashboardService(IAppDbContext db, IClock clock) : IDashboardService
{
    public async Task<IReadOnlyList<TodayHabitDto>> GetTodayAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var today = TimezoneHelper.LocalDate(clock.UtcNow, user.TimeZone);

        var habits = await db.Habits.AsNoTracking()
            .Where(h => h.UserId == userId && !h.IsArchived)
            .OrderBy(h => h.SortOrder).ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var result = new List<TodayHabitDto>();
        foreach (var habit in habits.Where(h => h.IsScheduledOn(today)))
        {
            var threshold = habit.CompletionThreshold;
            var todayValue = await db.CheckIns.AsNoTracking()
                .Where(c => c.HabitId == habit.Id && c.Date == today)
                .Select(c => (decimal?)c.Value)
                .FirstOrDefaultAsync(ct) ?? 0;

            var streak = await CheckInService.ComputeStreakAsync(db, habit, today, ct);

            var weekStart = StreakCalculator.WeekStart(today);
            var weekCompletions = habit.ScheduleType == Domain.Enums.ScheduleType.TimesPerWeek
                ? await db.CheckIns.AsNoTracking()
                    .CountAsync(c => c.HabitId == habit.Id
                                     && c.Date >= weekStart && c.Date <= today
                                     && c.Value >= threshold && c.Value > 0, ct)
                : 0;

            result.Add(new TodayHabitDto(
                habit.ToDto(),
                todayValue,
                todayValue > 0 && todayValue >= threshold,
                streak.Current,
                streak.Unit,
                weekCompletions));
        }

        return result;
    }

    public async Task<WeekOverviewDto> GetWeekAsync(Guid userId, DateOnly? weekStart, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var today = TimezoneHelper.LocalDate(clock.UtcNow, user.TimeZone);
        var start = weekStart is { } w ? StreakCalculator.WeekStart(w) : StreakCalculator.WeekStart(today);
        var end = start.AddDays(6);

        var habits = await db.Habits.AsNoTracking()
            .Where(h => h.UserId == userId && !h.IsArchived)
            .OrderBy(h => h.SortOrder).ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var checkIns = await db.CheckIns.AsNoTracking()
            .Where(c => c.UserId == userId && c.Date >= start && c.Date <= end)
            .ToListAsync(ct);
        var byHabitAndDate = checkIns.ToDictionary(c => (c.HabitId, c.Date), c => c.Value);

        var rows = habits.Select(habit =>
        {
            var threshold = habit.CompletionThreshold;
            var cells = Enumerable.Range(0, 7).Select(offset =>
            {
                var date = start.AddDays(offset);
                var value = byHabitAndDate.GetValueOrDefault((habit.Id, date));
                return new WeekDayCellDto(date, value, value > 0 && value >= threshold, habit.IsScheduledOn(date));
            }).ToList();
            return new WeekHabitRowDto(habit.ToDto(), cells);
        }).ToList();

        return new WeekOverviewDto(start, rows);
    }
}
