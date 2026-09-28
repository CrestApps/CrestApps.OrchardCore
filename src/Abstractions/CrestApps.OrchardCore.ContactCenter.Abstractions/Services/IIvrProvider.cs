namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Plays a phone menu to a caller and collects the key they press. The flow decides what the menu says; this is
/// what makes the caller hear it and reports what they pressed back through the provider's own events.
/// </summary>
public interface IIvrProvider
{
    /// <summary>
    /// Answers the caller's ringing leg so a menu can be played on it. A menu cannot be heard on a call nobody has
    /// picked up, and providers refuse to collect digits on one.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider accepted the command.</returns>
    Task<bool> AnswerAsync(string providerCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays a menu and collects a key.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">The menu to speak, when there is no media.</param>
    /// <param name="mediaId">Recorded audio to play instead of speaking.</param>
    /// <param name="validDigits">The keys this menu accepts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider accepted the command.</returns>
    Task<bool> PromptAsync(
        string providerCallId,
        string text,
        string mediaId,
        string validDigits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Speaks an entry point's welcome or closed message on an answered leg, before anything else happens to the caller.
    /// </summary>
    /// <remarks>
    /// The caller is only moved on (to the menu, the queue, voicemail) once the message has been said, so the
    /// provider reports the end of the speech through <see cref="IInboundVoiceDigitsSink.HandleAnnouncementEndedAsync"/>.
    /// When <paramref name="endCallAfter"/> is set nothing is reported: the provider ends the call itself once the
    /// message has been heard. A provider that cannot speak returns <see langword="false"/>, and the caller is moved on
    /// at once, exactly as if no message were configured.
    /// </remarks>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="text">What to say.</param>
    /// <param name="endCallAfter">Whether the call is ended once the message has been said.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider accepted the command.</returns>
    Task<bool> AnnounceAsync(
        string providerCallId,
        string text,
        bool endCallAfter,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
