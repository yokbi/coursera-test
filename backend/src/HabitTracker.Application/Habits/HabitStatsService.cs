using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Habits;

public sealed record HeatmapDayDto(DateOnly Date, decimal Value, bool Completed, bool Scheduled);

public sealed record HabitStatsDto(
    Guid HabitId,
    int CurrentStreak,
    int LongestStreak,
    string StreakUnit,
    double CompletionRate30,
    double CompletionRate90,
    int TotalCheckIns,
    IReadOnlyList<HeatmapDayDto> Heatmap90);

public interface IHabitStatsService
{
    Task<HabitStatsDto> GetStatsAsync(Guid userId, Guid habitId, CancellationToken ct = default);
}

public class HabitStatsService(IAppDbContext db, IClock clock) : IHabitStatsService
{
    public async Task<HabitStatsDto> GetStatsAsync(Guid userId, Guid habitId, CancellationToken ct = default)
    {
        var habit = await HabitService.FindOwnedHabitAsync(db, userId, habitId, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var today = TimezoneHelper.LocalDate(clock.UtcNow, user.TimeZone);
        var threshold = habit.CompletionThreshold;

        var allCheckIns = await db.CheckIns.AsNoTracking()
            .Where(c => c.HabitId == habitId)
            .Select(c => new { c.Date, c.Value })
            .ToListAsync(ct);

        var completedDates = allCheckIns
            .Where(c => c.Value > 0 && c.Value >= threshold)
            .Select(c => c.Date)
            .ToList();

        var effectiveStart = CheckIns.CheckInService.EffectiveStart(habit.StartDate, completedDates);
        var streak = StreakCalculator.Calculate(
            habit.ScheduleType, habit.ScheduleDays, habit.TimesPerWeek,
            completedDates, today, effectiveStart);

        var rate30 = StatsCalculator.CompletionRate(
            habit.ScheduleType, habit.ScheduleDays, habit.TimesPerWeek,
            completedDates, today.AddDays(-29), today, effectiveStart);
        var rate90 = StatsCalculator.CompletionRate(
            habit.ScheduleType, habit.ScheduleDays, habit.TimesPerWeek,
            completedDates, today.AddDays(-89), today, effectiveStart);

        var valuesByDate = allCheckIns.ToDictionary(c => c.Date, c => c.Value);
        var heatmap = StatsCalculator.Heatmap(
                habit.ScheduleType, habit.ScheduleDays, threshold,
                valuesByDate, today.AddDays(-89), today)
            .Select(d => new HeatmapDayDto(d.Date, d.Value, d.Completed, d.Scheduled))
            .ToList();

        return new HabitStatsDto(
            habit.Id, streak.Current, streak.Longest, streak.Unit,
            rate30, rate90, allCheckIns.Count, heatmap);
    }
}
