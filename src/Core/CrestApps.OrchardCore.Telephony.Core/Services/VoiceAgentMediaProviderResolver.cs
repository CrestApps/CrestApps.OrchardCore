namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="IVoiceAgentMediaProviderResolver"/>.
/// </summary>
public sealed class VoiceAgentMediaProviderResolver : IVoiceAgentMediaProviderResolver
{
    private readonly IVoiceAgentMediaProvider[] _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceAgentMediaProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered media providers.</param>
    public VoiceAgentMediaProviderResolver(IEnumerable<IVoiceAgentMediaProvider> providers)
    {
        _providers = providers.ToArray();
    }

    /// <inheritdoc/>
    public IVoiceAgentMediaProvider Get(string technicalName)
    {
        if (string.IsNullOrWhiteSpace(technicalName))
        {
            return null;
        }

        return _providers.FirstOrDefault(provider =>
            string.Equals(provider.TechnicalName, technicalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IVoiceAgentMediaProvider GetDefault()
        => _providers.Length == 1 ? _providers[0] : null;
}
