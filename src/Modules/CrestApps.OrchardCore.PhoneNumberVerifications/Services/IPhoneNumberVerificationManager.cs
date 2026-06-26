using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Coordinates phone number verification by resolving providers, selecting the active
/// provider, applying caching rules, and handling just-in-time verification.
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

    /// <summary>
    /// Verifies the phone number for a content item, applying caching rules and persisting the result.
    /// </summary>
    /// <param name="contentItem">The content item to verify.</param>
    /// <param name="options">Optional verification options.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The verification result, or <see langword="null"/> when no phone number could be resolved.</returns>
    Task<PhoneNumberVerificationResult> VerifyContentItemAsync(
        ContentItem contentItem,
        PhoneNumberVerificationOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a content item has a current verification result, performing just-in-time verification
    /// only when it is enabled in settings and the stored result has expired.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The current verification result, or <see langword="null"/> when none is available.</returns>
    Task<PhoneNumberVerificationResult> EnsureVerifiedAsync(
        ContentItem contentItem,
        CancellationToken cancellationToken = default);
}
