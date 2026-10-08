using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;

/// <summary>
/// Maps <see cref="MessageTemplate"/> documents to the <see cref="MessageTemplateIndex"/>.
/// </summary>
public sealed class MessageTemplateIndexProvider : IndexProvider<MessageTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTemplateIndexProvider"/> class.
    /// </summary>
    public MessageTemplateIndexProvider()
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MessageTemplate> context)
    {
        context
            .For<MessageTemplateIndex>()
            .Map(template => new MessageTemplateIndex
            {
                ItemId = template.ItemId,
                Name = template.Name,
            });
    }
}
