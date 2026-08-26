using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Habit> Habits { get; }
    DbSet<CheckIn> CheckIns { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Friendship> Friendships { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
