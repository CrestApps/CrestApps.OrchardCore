using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Transfers a live AI call to a phone number, cold, warm or screened.
/// </summary>
public interface IAIVoiceCallTransfers
{
    /// <summary>
    /// Transfers the call. The AI finishes its sentence first on a live session; on a turn-based call it stops at once.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="request">The number, how to transfer, and who asked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the transfer started, and what to tell whoever asked.</returns>
    Task<AIVoiceCallActionResult> TransferAsync(string activityId, AITakeOverRequest request, CancellationToken cancellationToken = default);
}
