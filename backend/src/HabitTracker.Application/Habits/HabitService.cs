using FluentValidation;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Habits;

public interface IHabitService
{
    Task<PagedResult<HabitDto>> ListAsync(Guid userId, bool includeArchived, int page, int pageSize, CancellationToken ct = default);
    Task<HabitDto> GetAsync(Guid userId, Guid habitId, CancellationToken ct = default);
    Task<HabitDto> CreateAsync(Guid userId, CreateHabitRequest request, CancellationToken ct = default);
    Task<HabitDto> UpdateAsync(Guid userId, Guid habitId, UpdateHabitRequest request, CancellationToken ct = default);
    Task<HabitDto> ArchiveAsync(Guid userId, Guid habitId, bool archived, CancellationToken ct = default);
    Task ReorderAsync(Guid userId, ReorderHabitsRequest request, CancellationToken ct = default);
}

public class HabitService(
    IAppDbContext db,
    IClock clock,
    IValidator<CreateHabitRequest> createValidator,
    IValidator<UpdateHabitRequest> updateValidator,
    IValidator<ReorderHabitsRequest> reorderValidator) : IHabitService
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<HabitDto>> ListAsync(Guid userId, bool includeArchived, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Habits.AsNoTracking().Where(h => h.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(h => !h.IsArchived);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(h => h.IsArchived).ThenBy(h => h.SortOrder).ThenBy(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<HabitDto>(items.Select(h => h.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<HabitDto> GetAsync(Guid userId, Guid habitId, CancellationToken ct = default)
    {
        var habit = await FindOwnedAsync(userId, habitId, ct);
        return habit.ToDto();
    }

    public async Task<HabitDto> CreateAsync(Guid userId, CreateHabitRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);

        var maxSortOrder = await db.Habits
            .Where(h => h.UserId == userId)
            .Select(h => (int?)h.SortOrder)
            .MaxAsync(ct) ?? -1;

        var habit = new Habit
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Description = NullIfBlank(request.Description),
            Color = request.Color,
            Icon = request.Icon,
            Category = NullIfBlank(request.Category),
            Type = request.Type,
            TargetValue = request.Type == Domain.Enums.HabitType.Boolean ? null : request.TargetValue,
            Unit = request.Type == Domain.Enums.HabitType.Quantity ? NullIfBlank(request.Unit) : null,
            ScheduleType = request.ScheduleType,
            ScheduleDays = request.ScheduleType == Domain.Enums.ScheduleType.SpecificWeekdays
                ? WeekdaysMapper.FromStrings(request.ScheduleDays)
                : Domain.Enums.Weekdays.None,
            TimesPerWeek = request.ScheduleType == Domain.Enums.ScheduleType.TimesPerWeek ? request.TimesPerWeek : null,
            SortOrder = maxSortOrder + 1,
            StartDate = TimezoneHelper.LocalDate(clock.UtcNow, user.TimeZone)
        };

        db.Habits.Add(habit);
        await db.SaveChangesAsync(ct);
        return habit.ToDto();
    }

    public async Task<HabitDto> UpdateAsync(Guid userId, Guid habitId, UpdateHabitRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var habit = await FindOwnedAsync(userId, habitId, ct);

        if (habit.Type is Domain.Enums.HabitType.Quantity or Domain.Enums.HabitType.Duration && request.TargetValue is null)
        {
            throw new ValidationException("Quantity and duration habits require a target value.",
                [new FluentValidation.Results.ValidationFailure("targetValue", "Quantity and duration habits require a target value.")]);
        }

        if (habit.Type is Domain.Enums.HabitType.Quantity && string.IsNullOrWhiteSpace(request.Unit))
        {
            throw new ValidationException("Quantity habits require a unit.",
                [new FluentValidation.Results.ValidationFailure("unit", "Quantity habits require a unit.")]);
        }

        habit.Name = request.Name.Trim();
        habit.Description = NullIfBlank(request.Description);
        habit.Color = request.Color;
        habit.Icon = request.Icon;
        habit.Category = NullIfBlank(request.Category);
        habit.TargetValue = habit.Type == Domain.Enums.HabitType.Boolean ? null : request.TargetValue;
        habit.Unit = habit.Type == Domain.Enums.HabitType.Quantity ? NullIfBlank(request.Unit) : null;
        habit.ScheduleType = request.ScheduleType;
        habit.ScheduleDays = request.ScheduleType == Domain.Enums.ScheduleType.SpecificWeekdays
            ? WeekdaysMapper.FromStrings(request.ScheduleDays)
            : Domain.Enums.Weekdays.None;
        habit.TimesPerWeek = request.ScheduleType == Domain.Enums.ScheduleType.TimesPerWeek ? request.TimesPerWeek : null;

        await db.SaveChangesAsync(ct);
        return habit.ToDto();
    }

    public async Task<HabitDto> ArchiveAsync(Guid userId, Guid habitId, bool archived, CancellationToken ct = default)
    {
        var habit = await FindOwnedAsync(userId, habitId, ct);
        habit.IsArchived = archived;
        habit.ArchivedAt = archived ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return habit.ToDto();
    }

    public async Task ReorderAsync(Guid userId, ReorderHabitsRequest request, CancellationToken ct = default)
    {
        await reorderValidator.ValidateAndThrowAsync(request, ct);
        var habits = await db.Habits
            .Where(h => h.UserId == userId && request.HabitIds.Contains(h.Id))
            .ToListAsync(ct);

        if (habits.Count != request.HabitIds.Count)
        {
            throw new NotFoundException("One or more habits were not found.");
        }

        var orderById = request.HabitIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        foreach (var habit in habits)
        {
            habit.SortOrder = orderById[habit.Id];
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ownership check: a habit belonging to another user is indistinguishable from a missing one.</summary>
    internal static async Task<Habit> FindOwnedHabitAsync(IAppDbContext db, Guid userId, Guid habitId, CancellationToken ct)
    {
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, ct);
        return habit ?? throw new NotFoundException("Habit not found.");
    }

    private Task<Habit> FindOwnedAsync(Guid userId, Guid habitId, CancellationToken ct) =>
        FindOwnedHabitAsync(db, userId, habitId, ct);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
