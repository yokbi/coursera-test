using FluentValidation;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Application.Habits;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.CheckIns;

public interface ICheckInService
{
    Task<CheckInResultDto> UpsertAsync(Guid userId, Guid habitId, UpsertCheckInRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CheckInDto>> ListAsync(Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public class CheckInService(
    IAppDbContext db,
    IClock clock,
    IValidator<UpsertCheckInRequest> validator) : ICheckInService
{
    public const int BackfillWindowDays = 7;

    public async Task<CheckInResultDto> UpsertAsync(Guid userId, Guid habitId, UpsertCheckInRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var habit = await HabitService.FindOwnedHabitAsync(db, userId, habitId, ct);
        if (habit.IsArchived)
        {
            throw new DomainRuleException("Archived habits cannot be checked in.");
        }

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var today = TimezoneHelper.LocalDate(clock.UtcNow, user.TimeZone);

        if (request.Date > today)
        {
            throw new DomainRuleException("Future days cannot be checked in.");
        }

        if (request.Date < today.AddDays(-BackfillWindowDays))
        {
            throw new DomainRuleException($"Check-ins can only be edited up to {BackfillWindowDays} days back.");
        }

        var checkIn = await db.CheckIns
            .FirstOrDefaultAsync(c => c.HabitId == habitId && c.Date == request.Date, ct);

        var newValue = request.Increment
            ? (checkIn?.Value ?? 0) + request.Value
            : request.Value;
        newValue = Math.Max(0, newValue);

        if (habit.Type == Domain.Enums.HabitType.Boolean)
        {
            newValue = newValue >= 1 ? 1 : 0;
        }

        if (newValue == 0)
        {
            // A zeroed day is equivalent to "no check-in": drop the row so stats stay clean.
            if (checkIn is not null)
            {
                db.CheckIns.Remove(checkIn);
                await db.SaveChangesAsync(ct);
            }
        }
        else if (checkIn is null)
        {
            checkIn = new CheckIn { HabitId = habitId, UserId = userId, Date = request.Date, Value = newValue };
            db.CheckIns.Add(checkIn);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            checkIn.Value = newValue;
            await db.SaveChangesAsync(ct);
        }

        var streak = await ComputeStreakAsync(db, habit, today, ct);
        var completed = newValue > 0 && newValue >= habit.CompletionThreshold;
        return new CheckInResultDto(new CheckInDto(request.Date, newValue, completed), streak.Current, streak.Unit);
    }

    public async Task<IReadOnlyList<CheckInDto>> ListAsync(Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var habit = await HabitService.FindOwnedHabitAsync(db, userId, habitId, ct);
        var threshold = habit.CompletionThreshold;

        var query = db.CheckIns.AsNoTracking().Where(c => c.HabitId == habitId);
        if (from is { } f)
        {
            query = query.Where(c => c.Date >= f);
        }

        if (to is { } t)
        {
            query = query.Where(c => c.Date <= t);
        }

        var checkIns = await query.OrderBy(c => c.Date).ToListAsync(ct);
        return checkIns
            .Select(c => new CheckInDto(c.Date, c.Value, c.Value > 0 && c.Value >= threshold))
            .ToList();
    }

    internal static async Task<StreakResult> ComputeStreakAsync(IAppDbContext db, Habit habit, DateOnly today, CancellationToken ct)
    {
        var threshold = habit.CompletionThreshold;
        var completedDates = await db.CheckIns.AsNoTracking()
            .Where(c => c.HabitId == habit.Id && c.Value >= threshold && c.Value > 0)
            .Select(c => c.Date)
            .ToListAsync(ct);

        return StreakCalculator.Calculate(
            habit.ScheduleType, habit.ScheduleDays, habit.TimesPerWeek,
            completedDates, today, EffectiveStart(habit.StartDate, completedDates));
    }

    /// <summary>Backfilled check-ins may predate the habit's creation day; streaks must honor them.</summary>
    internal static DateOnly EffectiveStart(DateOnly startDate, IReadOnlyCollection<DateOnly> completedDates)
    {
        if (completedDates.Count == 0)
        {
            return startDate;
        }

        var earliest = completedDates.Min();
        return earliest < startDate ? earliest : startDate;
    }
}

public class UpsertCheckInRequestValidator : AbstractValidator<UpsertCheckInRequest>
{
    public UpsertCheckInRequestValidator()
    {
        RuleFor(x => x.Value).InclusiveBetween(-100000, 100000);
        RuleFor(x => x.Date).NotEmpty();
    }
}
