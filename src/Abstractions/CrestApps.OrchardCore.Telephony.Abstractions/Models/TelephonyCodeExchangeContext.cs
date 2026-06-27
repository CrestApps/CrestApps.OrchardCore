namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Provides the context required to exchange an OAuth authorization code for tokens.
/// </summary>
public sealed class TelephonyCodeExchangeContext
{
    /// <summary>
    /// Gets or sets the authorization code returned by the provider.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the callback URL that was used when the authorization request was created.
    /// </summary>
    public string RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the PKCE code verifier that corresponds to the code challenge sent during
    /// authorization, or <see langword="null"/> when PKCE is not used.
    /// </summary>
    public string CodeVerifier { get; set; }
}
