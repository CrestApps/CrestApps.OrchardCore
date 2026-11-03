namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// Kept on an AI call's activity when the AI transferred the caller to a phone number, so the call is reported as
/// transferred rather than handed to an agent.
/// </summary>
public sealed class AICallTransfer
{
    /// <summary>
    /// Gets or sets the number the call was transferred to.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets when the call was transferred.
    /// </summary>
    public DateTime TransferredUtc { get; set; }
}
