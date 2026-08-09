using FluentValidation;
using HabitTracker.Application.Auth;
using HabitTracker.Application.CheckIns;
using HabitTracker.Application.Habits;
using Microsoft.Extensions.DependencyInjection;

namespace HabitTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IHabitService, HabitService>();
        services.AddScoped<ICheckInService, CheckInService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IHabitStatsService, HabitStatsService>();
        services.AddScoped<Reminders.IReminderService, Reminders.ReminderService>();
        return services;
    }
}
