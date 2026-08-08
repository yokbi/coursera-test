using FluentValidation;
using HabitTracker.Domain.Services;

namespace HabitTracker.Application.Auth;

public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinLength)
            .MaximumLength(MaxLength)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.TimeZone)
            .MaximumLength(100)
            .Must(tz => tz is null || TimezoneHelper.IsValidTimeZone(tz))
            .WithMessage("Unknown IANA timezone id.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        // May be empty for a Google-only account setting its first password; the
        // service still verifies it against the stored hash when one exists.
        RuleFor(x => x.CurrentPassword).MaximumLength(PasswordRules.MaxLength);
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

public class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        // Empty is allowed only for accounts without a password (Google-only);
        // the service still verifies it whenever a hash exists.
        RuleFor(x => x.Password).MaximumLength(PasswordRules.MaxLength);
    }
}
