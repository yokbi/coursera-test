using System.Security.Cryptography;
using System.Text;
using HabitTracker.Application.Reminders;
using Microsoft.Extensions.Options;

namespace HabitTracker.Infrastructure.Services;

/// <summary>
/// `{userId}.{hmac}` signed with the server key. Bearing the token only ever turns
/// reminders off, so it grants no account access even if a mail archive leaks.
/// </summary>
public class UnsubscribeTokenService(
    IOptions<JwtOptions> jwtOptions,
    IOptions<EmailOptions> emailOptions) : IUnsubscribeTokenService
{
    private const string Purpose = "unsubscribe:";
    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey);
    private readonly string _apiBaseUrl = emailOptions.Value.ApiBaseUrl.TrimEnd('/');

    public string CreateUnsubscribeUrl(Guid userId)
    {
        var id = userId.ToString("N");
        var token = $"{id}.{Base64UrlEncode(Sign(id))}";
        return $"{_apiBaseUrl}{ReminderService.UnsubscribePath}?token={Uri.EscapeDataString(token)}";
    }

    public Guid? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var separator = token.IndexOf('.');
        if (separator <= 0 || separator == token.Length - 1)
        {
            return null;
        }

        var id = token[..separator];
        if (!Guid.TryParseExact(id, "N", out var userId))
        {
            return null;
        }

        byte[] provided;
        try
        {
            provided = Base64UrlDecode(token[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = Sign(id);
        return CryptographicOperations.FixedTimeEquals(provided, expected) ? userId : null;
    }

    // Purpose-tagged so a signature can never be replayed as a different kind of token.
    private byte[] Sign(string id) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(Purpose + id));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
