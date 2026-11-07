namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// The channels consent is kept for. The values are the CRM's channel names, so a channel name read from an activity
/// or a conversation can be passed as it is.
/// </summary>
public static class ConsentChannels
{
    /// <summary>
    /// Phone calls.
    /// </summary>
    public const string Phone = "Phone";

    /// <summary>
    /// Text messages.
    /// </summary>
    public const string Sms = "SMS";

    /// <summary>
    /// E-mail.
    /// </summary>
    public const string Email = "Email";
}
