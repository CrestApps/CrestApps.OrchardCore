namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to transfer an active call to another destination.
/// </summary>
public sealed class TransferRequest
{
    /// <summary>
    /// Gets or sets the identifier of the call to transfer.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the destination phone number or address to transfer the call to.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the transfer mode that controls whether the agent speaks to the destination
    /// before completing the transfer.
    /// </summary>
    public TransferMode Mode { get; set; }
}
