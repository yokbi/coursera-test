using HabitTracker.Application.Common.Interfaces;

namespace HabitTracker.Infrastructure.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
