namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the result of a Contact Center voice provider operation.
/// </summary>
public sealed class ContactCenterVoiceProviderResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider operation succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier returned by the provider.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that executed the operation.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider error code when the operation failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the provider error message when the operation failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets provider-specific result metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
