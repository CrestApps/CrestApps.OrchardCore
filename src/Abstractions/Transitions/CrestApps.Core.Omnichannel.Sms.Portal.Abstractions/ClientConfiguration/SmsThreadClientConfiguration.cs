namespace CrestApps.Core.Omnichannel.Sms.Portal.ClientConfiguration;

/// <summary>
/// What one open SMS conversation in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// Separate from the inbox's configuration rather than a nullable section of it. The two pages share
/// only the hub, and a conversation's identifier is state about what is on screen rather than about
/// how to reach the server - folding it in is how one of these objects becomes a grab bag.
/// </remarks>
public sealed class SmsThreadClientConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the SMS portal hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the conversation the page is showing, which is the one it subscribes to.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the URL the page re-reads the thread from when a message arrives.
    /// </summary>
    public string MessagesUrl { get; set; }
}
