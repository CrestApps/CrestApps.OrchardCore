using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;

internal sealed class OmnichannelContactCommunicationPreferenceIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactCommunicationPreferenceIndex>()
            .Map(contentItem =>
            {
                var preference = contentItem.As<CommunicationPreferencePart>();

                if (preference == null)
                {
                    return null;
                }

                return new OmnichannelContactCommunicationPreferenceIndex()
                {
                    ContentItemId = contentItem.ContentItemId,

                    DoNotCall = preference.DoNotCall,
                    DoNotCallUtc = preference.DoNotCallUtc,

                    DoNotSms = preference.DoNotSms,
                    DoNotSmsUtc = preference.DoNotSmsUtc,

                    DoNotEmail = preference.DoNotEmail,
                    DoNotEmailUtc = preference.DoNotEmailUtc,

                    DoNotChat = preference.DoNotChat,
                    DoNotChatUtc = preference.DoNotChatUtc,
                };
            });
    }
}
