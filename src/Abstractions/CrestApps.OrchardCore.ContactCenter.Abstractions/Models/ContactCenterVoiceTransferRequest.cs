namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider request to transfer a live Contact Center call.
/// </summary>
public sealed class ContactCenterVoiceTransferRequest
{
    /// <summary>
    /// Gets or sets the interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the transfer type.
    /// </summary>
    public InteractionTransferType TransferType { get; set; }

    /// <summary>
    /// Gets or sets the destination type.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the provider-resolvable transfer destination.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
