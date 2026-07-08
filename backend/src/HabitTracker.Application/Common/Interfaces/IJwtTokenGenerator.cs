using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, int ExpiresInSeconds) GenerateAccessToken(User user);
}
