using System.Security.Claims;

namespace HabitTracker.Api.Infrastructure;

public static class CurrentUserExtensions
{
    /// <summary>Authenticated user id from the JWT `sub` claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("Authenticated principal has no subject claim.");
        return Guid.Parse(sub);
    }
}
