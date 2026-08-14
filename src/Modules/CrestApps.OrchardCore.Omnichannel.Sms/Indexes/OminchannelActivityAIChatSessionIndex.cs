using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Indexes;

/// <summary>
/// Represents the ominchannel activity AI chat session index.
/// </summary>
public sealed class OminchannelActivityAIChatSessionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the activity id.
    /// </summary>
    public string ActivityId { get; set; }
}
