namespace CrestApps.Core.Sms;

/// <summary>
/// A message to send over SMS.
/// </summary>
public sealed class SmsMessage
{
    /// <summary>
    /// Gets or sets the sending number, in E.164 form.
    /// </summary>
    /// <remarks>
    /// Also what a resolver keys on to choose the provider that owns the number, so a message sent
    /// from one carrier's number is not handed to another carrier.
    /// </remarks>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the destination number, in E.164 form.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    public string Body { get; set; }
}
