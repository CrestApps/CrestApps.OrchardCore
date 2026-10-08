using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates agent-initiated secure pause and resume of recording on the agent's own live interaction. It is
/// the agent-facing, ownership-checked, policy-gated boundary over <see cref="IContactCenterRecordingService"/>:
/// it verifies the tenant permits agent secure pause, that the caller owns the interaction, and that the provider
/// can pause recording, then applies the change and notifies connected supervisor and agent surfaces.
/// </summary>
public interface IAgentRecordingControlService
{
    /// <summary>
    /// Pauses recording on the caller's own live interaction for a sensitive-data capture, after verifying the
    /// tenant permits agent secure pause, the caller owns the interaction, and the provider supports pausing.
    /// </summary>
    /// <param name="interactionId">The interaction to pause recording on.</param>
    /// <param name="userId">The Orchard user identifier of the requesting agent.</param>
    /// <param name="principal">The authenticated principal of the requesting agent.</param>
    /// <param name="reason">The optional agent-supplied justification for the suppression gap.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the pause request.</returns>
    Task<AgentRecordingControlResult> PauseAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes recording on the caller's own live interaction after a sensitive-data capture, after verifying the
    /// tenant permits agent secure pause and the caller owns the interaction.
    /// </summary>
    /// <param name="interactionId">The interaction to resume recording on.</param>
    /// <param name="userId">The Orchard user identifier of the requesting agent.</param>
    /// <param name="principal">The authenticated principal of the requesting agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the resume request.</returns>
    Task<AgentRecordingControlResult> ResumeAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
