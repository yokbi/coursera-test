using System.Text;

namespace HabitTracker.Application.Reminders;

/// <summary>A habit awaiting the user, plus the streak that is on the line.</summary>
public sealed record PendingHabit(string Name, string Icon, int CurrentStreak, string StreakUnit);

/// <summary>
/// Builds the Turkish reminder mail. Pure so the copy and the escaping can be
/// unit tested; the app UI language is Turkish while the code stays English.
/// </summary>
public static class ReminderContentBuilder
{
    public static EmailMessage Build(string to, IReadOnlyList<PendingHabit> pending, string unsubscribeUrl)
    {
        var atRisk = pending.Count(h => h.CurrentStreak > 0);
        var subject = atRisk > 0
            ? $"🔥 {atRisk} serin risk altında"
            : $"Bugün {pending.Count} alışkanlığın seni bekliyor";

        var text = new StringBuilder();
        text.AppendLine("Merhaba,");
        text.AppendLine();
        text.AppendLine("Bugün tamamlanmayı bekleyen alışkanlıkların:");
        foreach (var habit in pending)
        {
            text.Append("- ").Append(habit.Icon).Append(' ').Append(habit.Name);
            if (habit.CurrentStreak > 0)
            {
                text.Append(" (").Append(StreakPhrase(habit)).Append(" seri sürüyor)");
            }

            text.AppendLine();
        }

        text.AppendLine();
        text.AppendLine($"Hatırlatmaları kapatmak için: {unsubscribeUrl}");

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"></head>");
        html.Append("<body style=\"font-family:sans-serif;line-height:1.5\">");
        html.Append("<p>Merhaba,</p><p>Bugün tamamlanmayı bekleyen alışkanlıkların:</p><ul>");
        foreach (var habit in pending)
        {
            html.Append("<li>")
                .Append(Escape(habit.Icon)).Append(' ')
                .Append("<strong>").Append(Escape(habit.Name)).Append("</strong>");
            if (habit.CurrentStreak > 0)
            {
                html.Append(" <span style=\"color:#b45309\">— ")
                    .Append(Escape(StreakPhrase(habit))).Append(" seri sürüyor</span>");
            }

            html.Append("</li>");
        }

        html.Append("</ul><p style=\"color:#64748b;font-size:12px\">")
            .Append("Hatırlatmaları kapatmak için <a href=\"")
            .Append(Escape(unsubscribeUrl))
            .Append("\">buraya tıkla</a>.</p></body></html>");

        return new EmailMessage(to, subject, text.ToString(), html.ToString());
    }

    /// <summary>Turkish vowel harmony picks the suffix: gün → günlük, hafta → haftalık.</summary>
    private static string StreakPhrase(PendingHabit habit) =>
        habit.StreakUnit == "weeks"
            ? $"{habit.CurrentStreak} haftalık"
            : $"{habit.CurrentStreak} günlük";

    /// <summary>
    /// Escapes only the characters that can break out of HTML text or an attribute.
    /// Unlike WebUtility.HtmlEncode this leaves Turkish letters and emoji intact,
    /// which the declared UTF-8 charset renders correctly.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");
}
