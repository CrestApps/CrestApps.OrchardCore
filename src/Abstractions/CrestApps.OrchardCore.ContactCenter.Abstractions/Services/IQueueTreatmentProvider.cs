namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Plays what a waiting caller hears on the provider that owns their call leg: a spoken message, hold music, or
/// a prompt that collects a key press. The policy decides what is due; this is what makes the caller hear it.
/// </summary>
public interface IQueueTreatmentProvider
{
    /// <summary>
    /// Speaks a message to the waiting caller.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts hold music on the waiting caller's leg.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="mediaId">The media reference to play.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offers the caller a choice and collects a single key press.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">The prompt.</param>
    /// <param name="acceptKey">The key that accepts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default);
}
