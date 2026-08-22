using CrestApps.OrchardCore.Sms.Workspace.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Sms.Workspace.Indexes;

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
        CollectionName = SmsWorkspaceStorage.CollectionName;
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
