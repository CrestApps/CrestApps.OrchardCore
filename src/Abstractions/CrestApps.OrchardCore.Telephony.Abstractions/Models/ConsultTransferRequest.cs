namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Names a transfer that is under way: the call being handed over, and the leg the transfer rang for it.
/// </summary>
public sealed class ConsultTransferRequest
{
    /// <summary>
    /// Gets or sets the identifier of the call being transferred.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the transfer's own leg, as the transfer's result named it in
    /// <see cref="TelephonyConstants.CallMetadata.ConsultId"/>.
    /// </summary>
    public string ConsultCallId { get; set; }
}
