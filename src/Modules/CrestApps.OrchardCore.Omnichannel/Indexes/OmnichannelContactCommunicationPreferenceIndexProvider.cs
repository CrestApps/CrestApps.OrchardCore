using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;

internal sealed class OmnichannelContactCommunicationPreferenceIndexProvider : IndexProvider<ContentItem>
{
    public OmnichannelContactCommunicationPreferenceIndexProvider()
    {
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactCommunicationPreferenceIndex>()
            .Map(contentItem =>
            {
                if (!contentItem.TryGet<OmnichannelContactPart>(out var contactPart))
                {
                    return null;
                }

                return new OmnichannelContactCommunicationPreferenceIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    DoNotCall = contactPart.DoNotCall,
                    DoNotCallUtc = contactPart.DoNotCallUtc,
                    DoNotSms = contactPart.DoNotSms,
                    DoNotSmsUtc = contactPart.DoNotSmsUtc,
                    DoNotEmail = contactPart.DoNotEmail,
                    DoNotEmailUtc = contactPart.DoNotEmailUtc,
                };
            });
    }
}
