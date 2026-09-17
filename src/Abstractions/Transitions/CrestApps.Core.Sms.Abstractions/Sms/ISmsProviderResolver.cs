namespace CrestApps.Core.Sms;

/// <summary>
/// Finds the SMS provider to send a message through.
/// </summary>
public interface ISmsProviderResolver
{
    /// <summary>
    /// Gets a provider by its technical name.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider, or <see langword="null"/> when none is registered or enabled under that name.</returns>
    Task<ISmsProvider> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the technical name of the provider to use when nothing more specific applies.
    /// </summary>
    /// <remarks>
    /// The name rather than the provider, because callers surface which provider was chosen and
    /// record it against the message.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default provider's technical name, or <see langword="null"/> when there is none.</returns>
    Task<string> GetDefaultProviderNameAsync(CancellationToken cancellationToken = default);
}
