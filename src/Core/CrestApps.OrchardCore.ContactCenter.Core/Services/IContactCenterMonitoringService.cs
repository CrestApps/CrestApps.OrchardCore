using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates scoped, audited supervisor engagement with live calls (monitor, whisper, barge, take
/// over). Engagement is gated by the voice provider's capabilities; provider modules execute the media.
/// </summary>
public interface IContactCenterMonitoringService
{
    /// <summary>
    /// Gets the executable supervisor engagement modes available for an interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The available engagement modes.</returns>
    Task<IReadOnlyCollection<MonitorMode>> GetAvailableModesAsync(
        string interactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Engages a live interaction as a supervisor using the requested mode when the provider supports it.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="supervisorId">The supervisor performing the engagement.</param>
    /// <param name="principal">The authenticated supervisor principal.</param>
    /// <param name="mode">The engagement mode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagement result.</returns>
    Task<SupervisorEngagementResult> EngageAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default);
}
