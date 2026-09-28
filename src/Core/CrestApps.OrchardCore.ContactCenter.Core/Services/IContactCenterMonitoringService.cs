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
    /// Gets the executable supervisor engagement modes available for an already-materialized interaction. This
    /// avoids reloading the interaction when the caller already holds it, such as a dashboard that has batch
    /// loaded every agent's active interaction.
    /// </summary>
    /// <param name="interaction">The interaction to evaluate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The available engagement modes.</returns>
    Task<IReadOnlyCollection<MonitorMode>> GetAvailableModesAsync(
        Interaction interaction,
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

    /// <summary>
    /// Stops a supervisor engagement on a live interaction, releasing only the supervisor-owned media without
    /// affecting the underlying customer-to-agent call. Authorized under the same boundary as
    /// <see cref="EngageAsync(string, string, ClaimsPrincipal, MonitorMode, CancellationToken)"/>.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="supervisorId">The supervisor whose engagement should be stopped.</param>
    /// <param name="principal">The authenticated supervisor principal.</param>
    /// <param name="mode">The engagement mode being stopped.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stop result.</returns>
    Task<SupervisorEngagementResult> StopEngagementAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-stops every live supervisor engagement on an interaction on behalf of the platform, without a
    /// supervisor principal or per-supervisor authorization. This is the enforcement path a secure pause uses to
    /// evict a supervisor who was already listening before a sensitive-data capture began, so the coaching legs
    /// are released along with the recording rather than only blocking new engagements.
    /// </summary>
    /// <param name="interactionId">The interaction identifier whose supervisor engagements should be released.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of live supervisor engagements the provider confirmed as stopped.</returns>
    Task<int> ForceDisengageAllAsync(
        string interactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-stops every live supervisor engagement on an interaction on behalf of the platform, recording why: the
    /// call is being moved somewhere its supervisors cannot follow (a transfer), or must not be heard (a secure pause).
    /// </summary>
    /// <param name="interactionId">The interaction identifier whose supervisor engagements should be released.</param>
    /// <param name="reason">Why the engagements were released, recorded on each stop event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of live supervisor engagements the provider confirmed as stopped.</returns>
    Task<int> ForceDisengageAllAsync(
        string interactionId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a supervisor's live engagement to another mode. A provider that can change the mode on the supervisor's
    /// existing leg does so without ringing them again; otherwise the engagement is stopped and started again in the
    /// new mode. Authorized under the same boundary as starting an engagement, and refused while a sensitive-data
    /// capture is in progress.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="supervisorId">The supervisor whose engagement changes.</param>
    /// <param name="principal">The authenticated supervisor principal.</param>
    /// <param name="mode">The new mode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> SwitchModeAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default);
}
