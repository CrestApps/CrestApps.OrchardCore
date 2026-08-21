using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Indexes;

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
        CollectionName = TelephonySmsStorage.CollectionName;
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
                Enabled = template.Enabled,
            });
    }
}
