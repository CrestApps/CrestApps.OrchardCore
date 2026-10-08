namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Hands an inbound call to an AI voice agent: answers it so that the provider's events for the call drive the automated
/// voice conversation of an activity. Registered by a voice provider's AI voice feature; an entry point can route calls
/// to an AI voice agent only when one is registered for the call's provider.
/// </summary>
public interface IInboundAIVoiceAnswerer
{
    /// <summary>
    /// Gets the technical name of the voice provider whose calls this answers.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Answers the call and ties it to the automated voice activity that holds the conversation.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="activityId">The automated activity the conversation is recorded on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the provider accepted the answer.</returns>
    Task<bool> AnswerAsync(string providerCallId, string activityId, CancellationToken cancellationToken = default);
}
