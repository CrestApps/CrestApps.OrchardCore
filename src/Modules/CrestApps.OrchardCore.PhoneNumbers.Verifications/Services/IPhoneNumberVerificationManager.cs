namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Resolves registered phone number verification providers and executes verification requests.
/// </summary>
public interface IPhoneNumberVerificationManager
{
    /// <summary>
    /// Gets the descriptors for all currently registered providers.
    /// </summary>
    /// <returns>The registered provider descriptors.</returns>
    IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor> GetProviders();

    /// <summary>
    /// Attempts to resolve a registered provider by its key.
    /// </summary>
    /// <param name="key">The provider key.</param>
    /// <param name="provider">When this method returns <see langword="true"/>, contains the resolved provider.</param>
    /// <returns><see langword="true"/> when the provider exists; otherwise, <see langword="false"/>.</returns>
    bool TryGetProvider(string key, out IPhoneNumberVerificationProvider provider);

    /// <summary>
    /// Resolves the key of the provider that should be used by default, falling back to the
    /// first registered provider when no explicit selection is configured.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The default provider key, or <see langword="null"/> when no provider is registered.</returns>
    Task<string> GetDefaultProviderKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a raw phone number using the specified or default provider, without applying caching.
    /// </summary>
    /// <param name="phoneNumber">The phone number to verify.</param>
    /// <param name="providerKey">The provider key to use, or <see langword="null"/> to use the default provider.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The verification result.</returns>
    Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        string providerKey = null,
        CancellationToken cancellationToken = default);
}
