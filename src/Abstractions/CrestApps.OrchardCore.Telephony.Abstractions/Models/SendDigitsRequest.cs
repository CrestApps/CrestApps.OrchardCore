namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to send DTMF digits to an active call.
/// </summary>
public sealed class SendDigitsRequest
{
    /// <summary>
    /// Gets or sets the identifier of the call to send the digits to.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the DTMF digits to send. Valid characters are 0-9, '*', '#', and 'A'-'D'.
    /// </summary>
    public string Digits { get; set; }
}
