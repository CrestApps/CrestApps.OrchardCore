namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// Describes why entering a child tenant did not work. The kinds are generic on purpose: an unknown child tenant and a
/// missing grant look the same.
/// </summary>
public enum EntryErrorKind
{
    /// <summary>
    /// The child tenant does not exist, cannot be entered now, or the user has no grant for it.
    /// </summary>
    NoAccess,

    /// <summary>
    /// The parent policy requires a sign-in with two-factor authentication.
    /// </summary>
    MfaRequired,

    /// <summary>
    /// The sign-in in the child tenant expired or was started in another browser.
    /// </summary>
    SignInExpired,
}
