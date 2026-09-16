using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Indexes;

/// <summary>
/// Maps <see cref="SmsTemplate"/> documents to the <see cref="SmsTemplateIndex"/>.
/// </summary>
public sealed class SmsTemplateIndexProvider : IndexProvider<SmsTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsTemplateIndexProvider"/> class.
    /// </summary>
    public SmsTemplateIndexProvider()
    {
        CollectionName = SmsPortalStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SmsTemplate> context)
    {
        context
            .For<SmsTemplateIndex>()
            .Map(template => new SmsTemplateIndex
            {
                ItemId = template.ItemId,
                Name = template.Name,
            });
    }
}
