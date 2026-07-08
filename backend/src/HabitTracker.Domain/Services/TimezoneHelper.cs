namespace HabitTracker.Domain.Services;

public static class TimezoneHelper
{
    /// <summary>Calendar day at the given UTC instant in the given IANA timezone.</summary>
    public static DateOnly LocalDate(DateTime utcInstant, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), tz);
        return DateOnly.FromDateTime(local);
    }

    public static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
