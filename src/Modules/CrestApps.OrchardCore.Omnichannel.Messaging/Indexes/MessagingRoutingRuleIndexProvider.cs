using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;

/// <summary>
/// Maps messaging routing rules to <see cref="MessagingRoutingRuleIndex"/> rows.
/// </summary>
internal sealed class MessagingRoutingRuleIndexProvider : IndexProvider<MessagingRoutingRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRuleIndexProvider"/> class.
    /// </summary>
    public MessagingRoutingRuleIndexProvider()
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MessagingRoutingRule> context)
    {
        context
            .For<MessagingRoutingRuleIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(rule => new MessagingRoutingRuleIndex
            {
                ItemId = rule.ItemId,
                DisplayText = string.IsNullOrEmpty(rule.DisplayText)
                    ? null
                    : rule.DisplayText[..Math.Min(255, rule.DisplayText.Length)],
                Enabled = rule.Enabled,
                Order = rule.Order,
            });
    }
}
