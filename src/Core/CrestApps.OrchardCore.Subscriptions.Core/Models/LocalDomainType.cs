namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Defines how a local domain name is supplied during tenant onboarding.
/// </summary>
public enum LocalDomainType
{
    /// <summary>
    /// Does not create or require a local domain.
    /// </summary>
    None,

    /// <summary>
    /// Generates a local domain without showing the field to the user.
    /// </summary>
    GeneratedHidden,

    /// <summary>
    /// Generates a local domain and shows it to the user.
    /// </summary>
    Generated,

    /// <summary>
    /// Uses the tenant name as a prefix for the local domain.
    /// </summary>
    Prefix,
};
