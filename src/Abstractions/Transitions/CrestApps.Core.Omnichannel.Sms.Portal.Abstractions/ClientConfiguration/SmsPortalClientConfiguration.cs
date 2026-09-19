namespace CrestApps.Core.Omnichannel.Sms.Portal.ClientConfiguration;

/// <summary>
/// What the SMS portal inbox in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// Unlike the other client surfaces, the portal's page carries these as separate attributes rather
/// than one serialized object, and its script reads them by name. The shape here is the contract; how
/// a host puts it in front of the script is the host's business.
/// </remarks>
public sealed class SmsPortalClientConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the SMS portal hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL of one conversation, with the identifier left as a token to substitute.
    /// </summary>
    public string ConversationUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the URL that records whether this agent is taking SMS work, or <see langword="null"/>
    /// when the caller has no agent profile to set it on.
    /// </summary>
    public string SetAvailabilityUrl { get; set; }

    /// <summary>
    /// Gets or sets the translated strings the inbox renders.
    /// </summary>
    /// <remarks>
    /// Filled by the host rather than by this suite. Translations are keyed on where the string was
    /// written as much as on the string itself, so moving these out of the host's own templates would
    /// leave every existing translation unreachable.
    /// </remarks>
    public IDictionary<string, string> Strings { get; set; }
}
