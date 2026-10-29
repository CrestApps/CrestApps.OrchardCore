namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a supervisor, or a workflow, can do to a call the AI is holding.
/// </summary>
public interface IAIVoiceCallSupervision
{
    /// <summary>
    /// Sends the AI a note to act on: a live session acts on it at its next quiet moment, and a turn-based call on
    /// its next turn.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="note">The note.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the AI will get the note.</returns>
    Task<bool> SendNoteAsync(string activityId, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the AI and rings the caller through to one agent.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="targetAgentId">The agent profile of the person taking the call.</param>
    /// <param name="requestedBy">The user name of the person asking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the call is being handed over, and why not when it is not.</returns>
    Task<AIVoiceCallActionResult> TakeOverAsync(string activityId, string targetAgentId, string requestedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the AI and transfers the caller to a phone number.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="phoneNumber">The number to ring, in E.164 form.</param>
    /// <param name="requestedBy">Who asked: a user name, or the workflow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the call is being transferred, and why not when it is not.</returns>
    Task<AIVoiceCallActionResult> TransferToNumberAsync(string activityId, string phoneNumber, string requestedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands the conversation to another AI profile, which answers next with the same
    /// transcript and variables. Only a turn-based call can change its profile; a live session keeps the one it
    /// was opened with.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="profileId">The AI profile to answer with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the profile changed, and why not when it did not.</returns>
    Task<AIVoiceCallActionResult> SwitchProfileAsync(string activityId, string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the call.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the call was ended, and why not when it was not.</returns>
    Task<AIVoiceCallActionResult> EndAsync(string activityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a supervisor's action on an AI call.
/// </summary>
/// <param name="Succeeded">Whether the action was carried out.</param>
/// <param name="Message">What happened, for the supervisor.</param>
public sealed record AIVoiceCallActionResult(bool Succeeded, string Message);
