namespace CrestApps.OrchardCore.PhoneNumberVerifications;

/// <summary>
/// Describes a registered phone number verification provider so the framework can
/// discover providers dynamically without referencing their implementations directly.
/// </summary>
public sealed class PhoneNumberVerificationProviderDescriptor
{
    /// <summary>
    /// Gets or sets the unique key that identifies the provider (e.g., <c>AbstractApi</c>).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name of the provider.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the human-readable description of the provider.
    /// </summary>
    public string Description { get; set; }
}
