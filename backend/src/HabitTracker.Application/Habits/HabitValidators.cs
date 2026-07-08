using FluentValidation;
using HabitTracker.Domain.Enums;

namespace HabitTracker.Application.Habits;

public static class HabitRuleExtensions
{
    public static void AddCommonHabitRules<T>(
        this AbstractValidator<T> validator,
        Func<T, string> name,
        Func<T, string?> description,
        Func<T, string> color,
        Func<T, string> icon,
        Func<T, string?> category,
        Func<T, HabitType> type,
        Func<T, decimal?> targetValue,
        Func<T, string?> unit,
        Func<T, ScheduleType> scheduleType,
        Func<T, IReadOnlyList<string>?> scheduleDays,
        Func<T, int?> timesPerWeek)
    {
        validator.RuleFor(x => name(x)).NotEmpty().MaximumLength(100).OverridePropertyName("name");
        validator.RuleFor(x => description(x)).MaximumLength(500).OverridePropertyName("description");
        validator.RuleFor(x => color(x)).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").OverridePropertyName("color");
        validator.RuleFor(x => icon(x)).NotEmpty().MaximumLength(16).OverridePropertyName("icon");
        validator.RuleFor(x => category(x)).MaximumLength(50).OverridePropertyName("category");
        validator.RuleFor(x => type(x)).IsInEnum().OverridePropertyName("type");
        validator.RuleFor(x => scheduleType(x)).IsInEnum().OverridePropertyName("scheduleType");

        validator.RuleFor(x => targetValue(x))
            .NotNull()
            .When(x => type(x) is HabitType.Quantity or HabitType.Duration, ApplyConditionTo.CurrentValidator)
            .WithMessage("Quantity and duration habits require a target value.")
            .OverridePropertyName("targetValue");
        validator.RuleFor(x => targetValue(x))
            .GreaterThan(0).LessThanOrEqualTo(100000)
            .When(x => targetValue(x) is not null)
            .OverridePropertyName("targetValue");

        validator.RuleFor(x => unit(x))
            .NotEmpty().When(x => type(x) == HabitType.Quantity)
            .WithMessage("Quantity habits require a unit.")
            .MaximumLength(30)
            .OverridePropertyName("unit");

        validator.RuleFor(x => scheduleDays(x))
            .Must(days => days is { Count: > 0 })
            .When(x => scheduleType(x) == ScheduleType.SpecificWeekdays)
            .WithMessage("Select at least one weekday.")
            .Must(WeekdaysMapper.AreValidNames)
            .WithMessage("Unknown weekday name.")
            .OverridePropertyName("scheduleDays");

        validator.RuleFor(x => timesPerWeek(x))
            .NotNull().InclusiveBetween(1, 7)
            .When(x => scheduleType(x) == ScheduleType.TimesPerWeek)
            .WithMessage("Times per week must be between 1 and 7.")
            .OverridePropertyName("timesPerWeek");
    }
}

public class CreateHabitRequestValidator : AbstractValidator<CreateHabitRequest>
{
    public CreateHabitRequestValidator()
    {
        this.AddCommonHabitRules(
            x => x.Name, x => x.Description, x => x.Color, x => x.Icon, x => x.Category,
            x => x.Type, x => x.TargetValue, x => x.Unit,
            x => x.ScheduleType, x => x.ScheduleDays, x => x.TimesPerWeek);
    }
}

public class UpdateHabitRequestValidator : AbstractValidator<UpdateHabitRequest>
{
    public UpdateHabitRequestValidator()
    {
        // Type is immutable after creation; updates reuse the stored type for conditional rules.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$");
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.ScheduleType).IsInEnum();
        RuleFor(x => x.TargetValue).GreaterThan(0).LessThanOrEqualTo(100000).When(x => x.TargetValue is not null);
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.ScheduleDays)
            .Must(days => days is { Count: > 0 })
            .When(x => x.ScheduleType == ScheduleType.SpecificWeekdays)
            .WithMessage("Select at least one weekday.")
            .Must(WeekdaysMapper.AreValidNames)
            .WithMessage("Unknown weekday name.");
        RuleFor(x => x.TimesPerWeek)
            .NotNull().InclusiveBetween(1, 7)
            .When(x => x.ScheduleType == ScheduleType.TimesPerWeek)
            .WithMessage("Times per week must be between 1 and 7.");
    }
}

public class ReorderHabitsRequestValidator : AbstractValidator<ReorderHabitsRequest>
{
    public ReorderHabitsRequestValidator()
    {
        RuleFor(x => x.HabitIds).NotEmpty();
        RuleFor(x => x.HabitIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Duplicate habit ids.");
    }
}
