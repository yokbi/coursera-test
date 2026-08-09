namespace HabitTracker.Application.Auth;

/// <summary>Identity returned by an external provider after a successful code exchange.</summary>
public sealed record ExternalIdentity(string Subject, string Email, bool EmailVerified);

/// <summary>
/// Exchanges an OAuth authorization code for the caller's identity. Kept behind an
/// interface so the flow can be exercised in tests without real provider credentials.
/// </summary>
public interface IExternalAuthClient
{
    /// <summary>Absolute URL to send the browser to in order to start consent.</summary>
    string BuildAuthorizationUrl(string state, string codeChallenge);

    /// <summary>Exchanges the code (with its PKCE verifier) and returns the verified identity.</summary>
    Task<ExternalIdentity> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default);
}
