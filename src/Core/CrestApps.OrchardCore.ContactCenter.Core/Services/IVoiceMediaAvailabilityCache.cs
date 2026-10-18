namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Remembers, for a bounded time, that a telephony provider confirmed it holds a copy of a clip, so the provider is
/// not asked again before every prompt, every hold-music start and every greeting. Only a confirmed presence is
/// remembered: an answer that the copy is missing, or no answer at all, is acted on at once and never cached.
/// </summary>
public interface IVoiceMediaAvailabilityCache
{
    /// <summary>
    /// Gets a value indicating whether the provider confirmed it holds the copy within the window, so the provider
    /// need not be asked again now.
    /// </summary>
    /// <param name="providerName">The provider's technical name.</param>
    /// <param name="mediaReference">The reference the provider holds the clip under.</param>
    /// <returns><see langword="true"/> when a confirmation is remembered and has not expired.</returns>
    bool IsKnownAvailable(string providerName, string mediaReference);

    /// <summary>
    /// Remembers that the provider holds the copy: it confirmed so, or it was just given the copy.
    /// </summary>
    /// <param name="providerName">The provider's technical name.</param>
    /// <param name="mediaReference">The reference the provider holds the clip under.</param>
    void MarkAvailable(string providerName, string mediaReference);

    /// <summary>
    /// Forgets what is remembered about the copy, because it was deleted or replaced.
    /// </summary>
    /// <param name="providerName">The provider's technical name.</param>
    /// <param name="mediaReference">The reference the provider held the clip under.</param>
    void Forget(string providerName, string mediaReference);
}
