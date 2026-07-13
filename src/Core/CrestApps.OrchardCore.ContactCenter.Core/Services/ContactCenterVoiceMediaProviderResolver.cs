using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default resolver for Contact Center live-media providers.
/// </summary>
public sealed class ContactCenterVoiceMediaProviderResolver : IContactCenterVoiceMediaProviderResolver
{
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IEnumerable<IContactCenterVoiceMediaProvider> _mediaProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaProviderResolver"/> class.
    /// </summary>
    /// <param name="voiceProviderResolver">The Contact Center voice provider resolver.</param>
    /// <param name="mediaProviders">The registered live-media providers.</param>
    public ContactCenterVoiceMediaProviderResolver(
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<IContactCenterVoiceMediaProvider> mediaProviders)
    {
        _voiceProviderResolver = voiceProviderResolver;
        _mediaProviders = mediaProviders;
    }

    /// <inheritdoc/>
    public IContactCenterVoiceMediaProvider Get(string technicalName = null)
    {
        var voiceProvider = _voiceProviderResolver.Get(technicalName);

        if (voiceProvider is null ||
            !voiceProvider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.BidirectionalMedia))
        {
            return null;
        }

        return _mediaProviders.FirstOrDefault(provider =>
            string.Equals(provider.TechnicalName, voiceProvider.TechnicalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IEnumerable<IContactCenterVoiceMediaProvider> GetAll()
    {
        var supportedProviderNames = _voiceProviderResolver.GetAll()
            .Where(provider => provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.BidirectionalMedia))
            .Select(provider => provider.TechnicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _mediaProviders.Where(provider => supportedProviderNames.Contains(provider.TechnicalName));
    }
}
