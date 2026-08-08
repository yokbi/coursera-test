using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HabitTracker.Application.Auth;
using HabitTracker.Application.Common.Exceptions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HabitTracker.Infrastructure.Services;

public class GoogleOAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>Must exactly match an authorized redirect URI in the Google console.</summary>
    public string RedirectUri { get; set; } = "http://localhost:5000/api/v1/auth/google/callback";
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Authorization-code flow with PKCE. The ID token comes straight from Google's
/// token endpoint over TLS, so its claims are read without a second signature
/// check — per Google's own guidance for the server-side flow.
/// </summary>
public class GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options) : IExternalAuthClient
{
    private readonly GoogleOAuthOptions _options = options.Value;

    private sealed class TokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        EnsureConfigured();
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            // Force account selection so switching Google accounts is possible.
            ["prompt"] = "select_account"
        };

        var encoded = string.Join('&', query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{_options.AuthorizationEndpoint}?{encoded}";
    }

    public async Task<ExternalIdentity> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        EnsureConfigured();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri
        });

        using var response = await httpClient.PostAsync(_options.TokenEndpoint, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Body may contain client details; keep it out of the client-facing error.
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (string.IsNullOrEmpty(token?.IdToken))
        {
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.IdToken);
        var subject = jwt.GetClaim("sub")?.Value;
        var email = jwt.GetClaim("email")?.Value;
        var emailVerified = jwt.TryGetClaim("email_verified", out var verifiedClaim) &&
                            string.Equals(verifiedClaim.Value, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
        {
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        return new ExternalIdentity(subject, email, emailVerified);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new DomainRuleException("Google sign-in is not configured on this server.");
        }
    }
}
