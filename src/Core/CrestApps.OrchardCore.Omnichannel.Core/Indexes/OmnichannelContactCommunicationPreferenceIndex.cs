using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the omnichannel contact communication preference index.
/// </summary>
public sealed class OmnichannelContactCommunicationPreferenceIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the do not call.
    /// </summary>
    public bool DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the do not call utc.
    /// </summary>
    public DateTime? DoNotCallUtc { get; set; }

    /// <summary>
    /// Gets or sets the do not sms.
    /// </summary>
    public bool DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets the do not sms utc.
    /// </summary>
    public DateTime? DoNotSmsUtc { get; set; }

    /// <summary>
    /// Gets or sets the do not email.
    /// </summary>
    public bool DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets the do not email utc.
    /// </summary>
    public DateTime? DoNotEmailUtc { get; set; }

    /// <summary>
    /// Gets or sets the do not chat.
    /// </summary>
    public bool DoNotChat { get; set; }

    /// <summary>
    /// Gets or sets the do not chat utc.
    /// </summary>
    public DateTime? DoNotChatUtc { get; set; }
}
