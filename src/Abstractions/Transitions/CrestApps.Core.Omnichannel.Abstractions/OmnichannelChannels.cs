namespace CrestApps.Core.Omnichannel;

/// <summary>
/// Names the channels a conversation can happen over.
/// </summary>
/// <remarks>
/// These values are stored: a channel endpoint, an activity and a message all record the channel they belong
/// to by one of these names, so changing a value orphans every record already written under the old one.
/// </remarks>
public static class OmnichannelChannels
{
    /// <summary>
    /// A voice call.
    /// </summary>
    public const string Phone = "Phone";

    /// <summary>
    /// A text message.
    /// </summary>
    public const string Sms = "SMS";

    /// <summary>
    /// An email message.
    /// </summary>
    public const string Email = "Email";
}
