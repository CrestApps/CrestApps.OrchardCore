using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Signs the platform's JSON Web Tokens and validates the tools' ones. LTI tokens are RS256 (Security Framework 6.1).
/// </summary>
public static class LtiTokens
{
    /// <summary>
    /// How far a tool's clock may be from the site's.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Signs claims as a compact JWT with <c>typ</c> JWT and the key's <c>kid</c>. The claims carry their own
    /// <c>iat</c> and <c>exp</c>; none are added.
    /// </summary>
    /// <param name="claims">The claims, JSON values as <see cref="JsonNode"/> or plain values.</param>
    /// <param name="credentials">The signing credentials.</param>
    /// <returns>The token.</returns>
    public static string Sign(IDictionary<string, object> claims, SigningCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(credentials);

        var payload = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (name, value) in claims)
        {
            if (value is null)
            {
                continue;
            }

            payload[name] = value is JsonNode node ? JsonSerializer.SerializeToElement(node) : value;
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Claims = payload,
            TokenType = "JWT",
            SigningCredentials = credentials,
        });
    }

    /// <summary>
    /// Validates a tool's token: an RS256 signature with one of the tool's keys, the issuer, the audience, and <c>exp</c>
    /// and <c>iat</c> (both required) against the clock, with <see cref="ClockSkew"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="issuer">The issuer the token must have (the tool's client id).</param>
    /// <param name="audience">The audience the token must have (the platform's issuer).</param>
    /// <param name="keys">The tool's keys.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The result; its claims when valid.</returns>
    public static async Task<TokenValidationResult> ValidateAsync(string token, string issuer, string audience, IEnumerable<SecurityKey> keys, DateTime utcNow)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new TokenValidationResult { IsValid = false, Exception = new SecurityTokenException("No token was given.") };
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKeys = keys?.ToArray() ?? [],
            TryAllIssuerSigningKeys = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            ClockSkew = ClockSkew,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires is { } expiry &&
                DateTime.SpecifyKind(expiry, DateTimeKind.Utc) + ClockSkew >= now &&
                (notBefore is null || DateTime.SpecifyKind(notBefore.Value, DateTimeKind.Utc) - ClockSkew <= now),
        });

        if (!result.IsValid)
        {
            return result;
        }

        if (result.SecurityToken is not JsonWebToken jwt ||
            !jwt.TryGetPayloadValue<long>("iat", out var issuedAt) ||
            DateTimeOffset.FromUnixTimeSeconds(issuedAt).UtcDateTime - ClockSkew > now)
        {
            return new TokenValidationResult { IsValid = false, Exception = new SecurityTokenException("The token has no valid iat claim.") };
        }

        return result;
    }
}
