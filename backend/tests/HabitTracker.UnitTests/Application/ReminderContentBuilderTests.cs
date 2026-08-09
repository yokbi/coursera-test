using HabitTracker.Application.Reminders;

namespace HabitTracker.UnitTests.Application;

public class ReminderContentBuilderTests
{
    private const string UnsubscribeUrl = "https://api.example.com/api/v1/reminders/unsubscribe?token=abc";

    [Fact]
    public void Subject_LeadsWithStreaksAtRisk_WhenThereAreAny()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Meditasyon", "🧘", 5, "days"), new PendingHabit("Su iç", "💧", 0, "days")],
            UnsubscribeUrl);

        Assert.Contains("1 serin risk altında", message.Subject);
    }

    [Fact]
    public void Subject_FallsBackToACount_WhenNoStreakIsAtRisk()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Su iç", "💧", 0, "days")],
            UnsubscribeUrl);

        Assert.Contains("1 alışkanlığın seni bekliyor", message.Subject);
    }

    [Fact]
    public void Body_ListsEveryPendingHabitInBothParts()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Meditasyon", "🧘", 3, "days"), new PendingHabit("Yüzme", "🏊", 2, "weeks")],
            UnsubscribeUrl);

        Assert.Contains("Meditasyon", message.TextBody);
        Assert.Contains("Yüzme", message.TextBody);
        Assert.Contains("Meditasyon", message.HtmlBody);
        Assert.Contains("Yüzme", message.HtmlBody);
    }

    [Fact]
    public void StreakUnit_IsLocalizedPerHabit()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Meditasyon", "🧘", 3, "days"), new PendingHabit("Yüzme", "🏊", 2, "weeks")],
            UnsubscribeUrl);

        Assert.Contains("3 günlük seri", message.TextBody);
        Assert.Contains("2 haftalık seri", message.TextBody);
    }

    [Fact]
    public void UnsubscribeLink_AppearsInBothParts()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Su iç", "💧", 0, "days")],
            UnsubscribeUrl);

        Assert.Contains(UnsubscribeUrl, message.TextBody);
        Assert.Contains("unsubscribe?token=abc", message.HtmlBody);
    }

    [Fact]
    public void HabitNames_AreHtmlEscaped()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("<script>alert(1)</script>", "💧", 0, "days")],
            UnsubscribeUrl);

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public void Recipient_IsCarriedThrough()
    {
        var message = ReminderContentBuilder.Build("kisi@example.com",
            [new PendingHabit("Su iç", "💧", 0, "days")],
            UnsubscribeUrl);

        Assert.Equal("kisi@example.com", message.To);
    }
}
