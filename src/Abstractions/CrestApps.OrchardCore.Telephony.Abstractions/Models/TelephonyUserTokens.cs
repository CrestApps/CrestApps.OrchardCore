namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the OAuth tokens issued to a user for a telephony provider.
/// </summary>
public sealed class TelephonyUserTokens
{
    /// <summary>
    /// Gets or sets the technical name of the provider that issued the tokens.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the access token used to authenticate provider API requests on behalf of the user.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token used to obtain a new access token when it expires.
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the time, in UTC, when the access token expires.
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the token type, for example <c>Bearer</c>.
    /// </summary>
    public string TokenType { get; set; }

    /// <summary>
    /// Gets or sets the scopes granted to the access token.
    /// </summary>
    public string Scope { get; set; }
}
