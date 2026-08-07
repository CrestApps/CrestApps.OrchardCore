namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default resolver for Contact Center voice providers.
/// </summary>
public sealed class ContactCenterVoiceProviderResolver : IContactCenterVoiceProviderResolver
{
    private readonly IEnumerable<IContactCenterVoiceProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered voice providers.</param>
    public ContactCenterVoiceProviderResolver(IEnumerable<IContactCenterVoiceProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public IContactCenterVoiceProvider Get(string technicalName = null)
    {
        if (string.IsNullOrEmpty(technicalName))
        {
            return _providers.FirstOrDefault();
        }

        return _providers.FirstOrDefault(provider => string.Equals(provider.TechnicalName, technicalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IEnumerable<IContactCenterVoiceProvider> GetAll()
    {
        return _providers;
    }
}
