namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

/// <summary>
/// Reports whether a specific phone number verification provider is currently enabled, allowing the
/// framework to filter providers without referencing their settings types directly.
/// </summary>
public interface IPhoneNumberVerificationProviderConfiguration
{
    /// <summary>
    /// Gets the key of the provider this configuration applies to.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Determines whether the provider is currently enabled.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the provider is enabled; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
}
