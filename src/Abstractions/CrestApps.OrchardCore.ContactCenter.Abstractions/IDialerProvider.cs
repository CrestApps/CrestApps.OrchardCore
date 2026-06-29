using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Defines a dialer-agnostic provider that executes outbound calling on behalf of the Contact Center.
/// The Contact Center owns all assignment, queue, pacing, and compliance logic; the provider only places
/// and ends calls so any telephony platform can act as the calling engine.
/// </summary>
public interface IDialerProvider
{
    /// <summary>
    /// Gets the stable technical name used to resolve the provider.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets the localized, human-readable name of the provider.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the calling capabilities supported by the provider.
    /// </summary>
    DialerProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Places an outbound call for a reserved activity.
    /// </summary>
    /// <param name="request">The provider-agnostic dial request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the dial operation.</returns>
    Task<DialerDialResult> PlaceCallAsync(DialerDialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends or cancels a provider call previously placed for an attempt.
    /// </summary>
    /// <param name="providerCallId">The provider call identifier returned by <see cref="PlaceCallAsync"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the end-call operation.</returns>
    Task<DialerDialResult> EndCallAsync(string providerCallId, CancellationToken cancellationToken = default);
}
