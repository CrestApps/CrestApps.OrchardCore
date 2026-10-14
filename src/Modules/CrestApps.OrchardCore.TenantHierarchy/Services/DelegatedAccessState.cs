namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Holds what a child tenant remembers between starting a delegated access sign-in and its callback. It travels in
/// a short-lived encrypted cookie, which binds the result to the browser that started the sign-in.
/// </summary>
public sealed class DelegatedAccessState
{
    /// <summary>
    /// Gets or sets the random state sent to the parent.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the PKCE code verifier.
    /// </summary>
    public string CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets the local URL to open after the sign-in.
    /// </summary>
    public string ReturnUrl { get; set; }
}
