using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Builds provider-specific browser soft-phone registration configuration for the active tenant.
/// </summary>
public interface ISoftPhoneRegistrationConfigContributor
{
    /// <summary>
    /// Gets the technical provider name handled by this contributor.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Builds a short-lived browser registration configuration for the current soft-phone session.
    /// </summary>
    /// <param name="context">The registration request context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registration configuration, or <see langword="null"/> when the provider is unavailable.</returns>
    Task<SoftPhoneRegistrationConfig> BuildAsync(
        SoftPhoneRegistrationConfigContext context,
        CancellationToken cancellationToken = default);
}
