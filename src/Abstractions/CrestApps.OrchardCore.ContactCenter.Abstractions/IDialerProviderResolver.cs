namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Resolves the registered <see cref="IDialerProvider"/> implementations so the Contact Center can
/// dial through any installed provider without depending on a specific telephony platform.
/// </summary>
public interface IDialerProviderResolver
{
    /// <summary>
    /// Resolves the dialer provider with the specified technical name, or the only registered provider when no name is supplied.
    /// </summary>
    /// <param name="technicalName">The provider technical name, or <see langword="null"/> to resolve the default.</param>
    /// <returns>The matching provider, or <see langword="null"/> when none is found.</returns>
    IDialerProvider Get(string technicalName = null);

    /// <summary>
    /// Gets every registered dialer provider.
    /// </summary>
    /// <returns>The registered dialer providers.</returns>
    IEnumerable<IDialerProvider> GetAll();
}
