using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Provides contextual information about a phone number verification operation.
/// </summary>
public sealed class PhoneNumberVerificationContext
{
    /// <summary>
    /// Gets or sets the phone number being verified.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider performing the verification.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the content item the verification is associated with, when applicable.
    /// </summary>
    public ContentItem ContentItem { get; set; }

    /// <summary>
    /// Gets or sets the verification result. This is <see langword="null"/> before verification completes.
    /// </summary>
    public PhoneNumberVerificationResult Result { get; set; }
}
