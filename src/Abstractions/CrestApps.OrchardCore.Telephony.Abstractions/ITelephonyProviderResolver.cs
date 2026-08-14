namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Resolves a registered <see cref="ITelephonyProvider"/> by its technical name.
/// </summary>
public interface ITelephonyProviderResolver
{
    /// <summary>
    /// Gets the telephony provider for the given technical name. When the name is <see langword="null"/>
    /// or empty, the configured default provider is returned.
    /// </summary>
    /// <param name="name">The technical name of the provider, or <see langword="null"/> for the default provider.</param>
    /// <returns>The resolved <see cref="ITelephonyProvider"/>, or <see langword="null"/> when no provider matches.</returns>
    Task<ITelephonyProvider> GetAsync(string name = null);
}
