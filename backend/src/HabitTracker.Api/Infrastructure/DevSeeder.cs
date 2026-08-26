using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Enums;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Api.Infrastructure;

/// <summary>Development-only demo data: one user with five habits and ~30 days of history.</summary>
public static class DevSeeder
{
    public const string DemoEmail = "demo@habittracker.local";
    public const string DemoPassword = "Demo1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasherService>();

        if (await db.Users.AnyAsync(u => u.Email == DemoEmail))
        {
            return;
        }

        var user = new User
        {
            Email = DemoEmail,
            PasswordHash = hasher.Hash(DemoPassword),
            TimeZone = "Europe/Istanbul",
            // The demo account doubles as the admin so the panel is reachable in dev.
            Role = UserRole.Admin
        };
        db.Users.Add(user);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone)));
        var start = today.AddDays(-30);

        var habits = new[]
        {
            new Habit
            {
                UserId = user.Id, Name = "Meditasyon", Description = "Sabah 10 dakika nefes egzersizi",
                Color = "#8b5cf6", Icon = "🧘", Category = "Sağlık",
                Type = HabitType.Boolean, ScheduleType = ScheduleType.Daily,
                SortOrder = 0, StartDate = start
            },
            new Habit
            {
                UserId = user.Id, Name = "Su iç", Description = "Günde 8 bardak su",
                Color = "#0ea5e9", Icon = "💧", Category = "Sağlık",
                Type = HabitType.Quantity, TargetValue = 8, Unit = "bardak",
                ScheduleType = ScheduleType.Daily, SortOrder = 1, StartDate = start
            },
            new Habit
            {
                UserId = user.Id, Name = "Kitap oku", Description = "Her akşam 30 dakika",
                Color = "#f59e0b", Icon = "📚", Category = "Gelişim",
                Type = HabitType.Duration, TargetValue = 30,
                ScheduleType = ScheduleType.Daily, SortOrder = 2, StartDate = start
            },
            new Habit
            {
                UserId = user.Id, Name = "Koşu", Description = "Pazartesi, Çarşamba, Cuma koşusu",
                Color = "#22c55e", Icon = "🏃", Category = "Spor",
                Type = HabitType.Duration, TargetValue = 45,
                ScheduleType = ScheduleType.SpecificWeekdays,
                ScheduleDays = Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday,
                SortOrder = 3, StartDate = start
            },
            new Habit
            {
                UserId = user.Id, Name = "Yüzme", Description = "Haftada 2 kez havuz",
                Color = "#06b6d4", Icon = "🏊", Category = "Spor",
                Type = HabitType.Boolean, ScheduleType = ScheduleType.TimesPerWeek,
                TimesPerWeek = 2, SortOrder = 4, StartDate = start
            }
        };
        db.Habits.AddRange(habits);

        // Deterministic pseudo-random history so the dashboard demos consistently.
        var rng = new Random(42);
        foreach (var habit in habits)
        {
            for (var day = start; day <= today; day = day.AddDays(1))
            {
                if (!habit.IsScheduledOn(day))
                {
                    continue;
                }

                if (habit.ScheduleType == ScheduleType.TimesPerWeek)
                {
                    // Roughly hit the weekly quota: check in on ~2 of 7 days.
                    if (rng.NextDouble() > 0.3)
                    {
                        continue;
                    }
                }
                else if (rng.NextDouble() > 0.85)
                {
                    continue; // occasional missed day keeps the data realistic
                }

                var target = habit.CompletionThreshold;
                var value = habit.Type == HabitType.Boolean
                    ? 1m
                    : Math.Round(target * (decimal)(0.6 + rng.NextDouble() * 0.6), 0);

                db.CheckIns.Add(new CheckIn
                {
                    HabitId = habit.Id,
                    UserId = user.Id,
                    Date = day,
                    Value = value
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
