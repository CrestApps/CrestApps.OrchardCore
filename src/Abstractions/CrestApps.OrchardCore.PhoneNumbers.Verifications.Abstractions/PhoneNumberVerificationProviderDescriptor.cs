using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications;

/// <summary>
/// Describes a registered phone number verification provider so the framework can
/// discover providers dynamically without referencing their implementations directly.
/// </summary>
public sealed class PhoneNumberVerificationProviderDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationProviderDescriptor"/> class.
    /// </summary>
    /// <param name="key">The unique key that identifies the provider.</param>
    public PhoneNumberVerificationProviderDescriptor(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        Key = key;
        DisplayName = new LocalizedString(key, key);
    }

    /// <summary>
    /// Gets the unique key that identifies the provider (e.g., <c>AbstractApi</c>).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets or sets the localized display name of the provider.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized description of the provider.
    /// </summary>
    public LocalizedString Description { get; set; }
}
