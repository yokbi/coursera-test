using FluentValidation;

namespace HabitTracker.Application.Groups;

public class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$");
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(16);
        RuleFor(x => x.HabitId).NotEmpty();
    }
}

public class JoinGroupRequestValidator : AbstractValidator<JoinGroupRequest>
{
    public JoinGroupRequestValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
    }
}
