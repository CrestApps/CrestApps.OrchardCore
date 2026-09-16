namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Bounds how long a direct-to-agent (personal line) call is held waiting for its named agent. Held calls whose
/// entry point enables voicemail are sent to voicemail once their ring window elapses; held calls whose entry
/// point disabled voicemail are re-offered to the agent whenever they are available.
/// </summary>
public interface IDirectHoldTimeoutService
{
    /// <summary>
    /// Processes every held direct-to-agent call that is due: sends elapsed ring windows to voicemail, and
    /// re-offers voicemail-disabled holds to their available agent.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of held calls acted on.</returns>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}
