using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Resolves the effective AI profile and speech settings for an automated phone activity.
/// </summary>
public interface IAutomatedVoiceActivitySettingsResolver
{
    /// <summary>
    /// Resolves activity overrides, subject-flow defaults, and site AI defaults in that order.
    /// </summary>
    /// <param name="activity">The automated phone activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective automated voice settings.</returns>
    Task<AutomatedVoiceActivitySettings> ResolveAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken = default);
}
