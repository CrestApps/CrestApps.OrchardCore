namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the result of building a provider OAuth authorization request, including the destination
/// URL and any PKCE code verifier that must be retained for the later code exchange.
/// </summary>
public sealed class TelephonyAuthorizationRequest
{
    /// <summary>
    /// Gets or sets the absolute authorization URL the user is redirected to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the PKCE code verifier generated for this request, or <see langword="null"/> when
    /// PKCE is not used. The caller must persist this value and supply it when exchanging the code.
    /// </summary>
    public string CodeVerifier { get; set; }
}
