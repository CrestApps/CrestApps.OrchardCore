namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves registered <see cref="IDialerProvider"/> implementations by technical name.
/// </summary>
public sealed class DialerProviderResolver : IDialerProviderResolver
{
    private readonly IEnumerable<IDialerProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered dialer providers.</param>
    public DialerProviderResolver(IEnumerable<IDialerProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public IDialerProvider Get(string technicalName = null)
    {
        if (string.IsNullOrEmpty(technicalName))
        {
            return _providers.FirstOrDefault();
        }

        return _providers.FirstOrDefault(provider => string.Equals(provider.TechnicalName, technicalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IEnumerable<IDialerProvider> GetAll()
    {
        return _providers;
    }
}
