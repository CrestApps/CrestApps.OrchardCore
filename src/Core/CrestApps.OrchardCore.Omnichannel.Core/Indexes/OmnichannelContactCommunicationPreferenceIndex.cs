using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelContactCommunicationPreferenceIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public bool DoNotCall { get; set; }

    public DateTime? DoNotCallUtc { get; set; }

    public bool DoNotSms { get; set; }

    public DateTime? DoNotSmsUtc { get; set; }

    public bool DoNotEmail { get; set; }

    public DateTime? DoNotEmailUtc { get; set; }

    public bool DoNotChat { get; set; }

    public DateTime? DoNotChatUtc { get; set; }
}
