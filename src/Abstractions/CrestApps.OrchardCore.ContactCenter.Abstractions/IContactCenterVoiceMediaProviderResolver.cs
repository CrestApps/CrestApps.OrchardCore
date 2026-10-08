namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Resolves registered live-media providers whose corresponding voice providers advertise bidirectional media support.
/// </summary>
public interface IContactCenterVoiceMediaProviderResolver
{
    /// <summary>
    /// Resolves the live-media provider with the specified technical name, or the default voice provider's
    /// media implementation when no name is supplied.
    /// </summary>
    /// <param name="technicalName">The provider technical name, or <see langword="null"/> to resolve the default.</param>
    /// <returns>The matching media provider, or <see langword="null"/> when bidirectional media is unavailable.</returns>
    IContactCenterVoiceMediaProvider Get(string technicalName = null);

    /// <summary>
    /// Gets every registered media provider whose corresponding voice provider advertises bidirectional media support.
    /// </summary>
    /// <returns>The available live-media providers.</returns>
    IEnumerable<IContactCenterVoiceMediaProvider> GetAll();
}
