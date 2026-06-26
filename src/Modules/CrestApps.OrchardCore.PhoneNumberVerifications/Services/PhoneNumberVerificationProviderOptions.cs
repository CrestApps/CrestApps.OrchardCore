namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Holds the set of registered phone number verification providers so the framework can
/// discover them dynamically without referencing their implementations.
/// </summary>
public sealed class PhoneNumberVerificationProviderOptions
{
    /// <summary>
    /// Gets the registered providers keyed by their unique provider key.
    /// </summary>
    public Dictionary<string, PhoneNumberVerificationProviderDescriptor> Providers { get; }
        = new(StringComparer.OrdinalIgnoreCase);
}
