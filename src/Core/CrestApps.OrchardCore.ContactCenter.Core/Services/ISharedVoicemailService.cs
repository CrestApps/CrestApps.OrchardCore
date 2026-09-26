using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Delivers voicemails to queue shared voicemail boxes, and carries out what a team does with them.
/// </summary>
/// <remarks>
/// Every request made on a user's behalf is authorized here, against the same access
/// <see cref="ISharedVoicemailAuthorizationService"/> resolves, so the list, the playback and every action enforce one
/// rule whichever endpoint asks. A message outside the user's queues is reported as not found, never as forbidden, so a
/// user cannot learn that another team's message exists. Every change is recorded in the Contact Center event log.
/// </remarks>
public interface ISharedVoicemailService
{
    /// <summary>
    /// Files a message in its queue's shared box. Delivering the same interaction again returns the message already
    /// filed for it, so a replayed event never files a message twice.
    /// </summary>
    /// <param name="voicemail">The message to file. <see cref="SharedVoicemail.InteractionId"/> and
    /// <see cref="SharedVoicemail.QueueId"/> are required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The filed message.</returns>
    Task<SharedVoicemail> DeliverAsync(SharedVoicemail voicemail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of the messages the user may see, newest first. The query's queues are narrowed to the user's
    /// queues; asking for a queue the user may not see reads nothing from it.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page; empty when the user may not use the shared boxes.</returns>
    Task<SharedVoicemailPage> ListAsync(ClaimsPrincipal principal, SharedVoicemailQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a message's recording for the user to listen to, through recording governance so the access is audited
    /// like every other recording access.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result, carrying the recording stream when it succeeded.</returns>
    Task<SharedVoicemailActionResult> OpenRecordingAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a message for the user: they will handle it, and the rest of the team can see that they are. A user who
    /// manages shared voicemail may take over somebody else's claim.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SharedVoicemailActionResult> ClaimAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a claimed or resolved message to the team, unclaimed. Only whoever holds it, or a user who manages
    /// shared voicemail, may return it.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SharedVoicemailActionResult> ReleaseAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as dealt with, with an optional note. An unclaimed message is claimed by the user as it is
    /// resolved; somebody else's claim can only be resolved by a user who manages shared voicemail.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="note">An optional note on how the message was dealt with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SharedVoicemailActionResult> ResolveAsync(ClaimsPrincipal principal, string voicemailId, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a callback to the caller, queued back into the message's queue so the next available member of the
    /// team places it. An unclaimed message is claimed by the user as the callback is requested.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result, carrying the scheduled callback when it succeeded.</returns>
    Task<SharedVoicemailActionResult> RequestCallbackAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a message and erases its recording through recording governance. It needs
    /// <see cref="ContactCenterPermissions.ManageSharedVoicemail"/>, and a recording under legal hold is never erased.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="voicemailId">The shared voicemail identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SharedVoicemailActionResult> DeleteAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default);
}
