namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Options that control how a content item verification is performed.
/// </summary>
public sealed class PhoneNumberVerificationOptions
{
    /// <summary>
    /// Gets or sets the provider key to use. When <see langword="null"/>, the default provider is used.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force verification even when a valid cached result exists.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who triggered the verification, if any.
    /// </summary>
    public string VerifiedByUserId { get; set; }
}
