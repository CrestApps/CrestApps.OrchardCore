namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Resolves registered <see cref="IContactCenterVoiceProvider"/> implementations by technical name.
/// </summary>
public interface IContactCenterVoiceProviderResolver
{
    /// <summary>
    /// Resolves the Contact Center voice provider with the specified technical name, or the default provider when no name is supplied.
    /// </summary>
    /// <param name="technicalName">The provider technical name, or <see langword="null"/> to resolve the default.</param>
    /// <returns>The matching provider, or <see langword="null"/> when none is found.</returns>
    IContactCenterVoiceProvider Get(string technicalName = null);

    /// <summary>
    /// Gets every registered Contact Center voice provider.
    /// </summary>
    /// <returns>The registered voice providers.</returns>
    IEnumerable<IContactCenterVoiceProvider> GetAll();
}
