using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelContactIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string PrimaryCellPhoneNumber { get; set; }

    public string PrimaryHomePhoneNumber { get; set; }

    public string PrimaryEmailAddress { get; set; }
}
