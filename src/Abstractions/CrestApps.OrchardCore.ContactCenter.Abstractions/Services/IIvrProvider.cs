namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Plays a phone menu to a caller and collects the key they press. The flow decides what the menu says; this is
/// what makes the caller hear it and reports what they pressed back through the provider's own events.
/// </summary>
public interface IIvrProvider
{
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
}
