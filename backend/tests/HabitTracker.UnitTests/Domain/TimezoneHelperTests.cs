using HabitTracker.Domain.Services;

namespace HabitTracker.UnitTests.Domain;

public class TimezoneHelperTests
{
    [Fact]
    public void LocalDate_UtcEveningIsNextDayInIstanbul()
    {
        // 22:30 UTC = 01:30 next day in Istanbul (UTC+3).
        var utc = new DateTime(2026, 7, 8, 22, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 9), TimezoneHelper.LocalDate(utc, "Europe/Istanbul"));
    }

    [Fact]
    public void LocalDate_UtcEarlyMorningIsPreviousDayInNewYork()
    {
        // 02:00 UTC = 22:00 previous day in New York (EDT, UTC-4).
        var utc = new DateTime(2026, 7, 9, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 8), TimezoneHelper.LocalDate(utc, "America/New_York"));
    }

    [Fact]
    public void LocalDate_MidnightBoundaryExactlyAtLocalMidnight()
    {
        // 21:00 UTC = exactly 00:00 in Istanbul (UTC+3).
        var utc = new DateTime(2026, 7, 8, 21, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 9), TimezoneHelper.LocalDate(utc, "Europe/Istanbul"));
    }

    [Fact]
    public void LocalDate_DstSpringForwardInNewYork()
    {
        // 2026-03-08 US DST starts; 07:00 UTC = 03:00 EDT same day (clock jumped 02:00→03:00).
        var utc = new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 3, 8), TimezoneHelper.LocalDate(utc, "America/New_York"));
    }

    [Fact]
    public void LocalDate_DstFallBackInBerlin_LateUtcStillSameLocalDay()
    {
        // 2026-10-25 EU DST ends (CEST→CET). 23:30 UTC = 00:30 CET on Oct 26.
        var utc = new DateTime(2026, 10, 25, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 10, 26), TimezoneHelper.LocalDate(utc, "Europe/Berlin"));
        // While 22:30 UTC is still 23:30 CET on Oct 25.
        var earlier = new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 10, 25), TimezoneHelper.LocalDate(earlier, "Europe/Berlin"));
    }

    [Theory]
    [InlineData("Europe/Istanbul", true)]
    [InlineData("America/New_York", true)]
    [InlineData("UTC", true)]
    [InlineData("Not/AZone", false)]
    [InlineData("", false)]
    public void IsValidTimeZone_Validates(string id, bool expected)
    {
        Assert.Equal(expected, TimezoneHelper.IsValidTimeZone(id));
    }
}
