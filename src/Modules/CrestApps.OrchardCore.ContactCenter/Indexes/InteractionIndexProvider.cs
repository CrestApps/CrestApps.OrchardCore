using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="Interaction"/> documents to the <see cref="InteractionIndex"/>.
/// </summary>
public sealed class InteractionIndexProvider : IndexProvider<Interaction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionIndexProvider"/> class.
    /// </summary>
    public InteractionIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Interaction> context)
    {
        context
            .For<InteractionIndex>()
            .Map(interaction => new InteractionIndex
            {
                ItemId = interaction.ItemId,
                Channel = interaction.Channel,
                Direction = interaction.Direction,
                Status = interaction.Status,
                ActivityItemId = interaction.ActivityItemId,
                ProviderName = interaction.ProviderName,
                ProviderInteractionId = interaction.ProviderInteractionId,
                ProviderLegId = interaction.ProviderLegId,
                QueueId = interaction.QueueId,
                AgentId = interaction.AgentId,
                CorrelationId = interaction.CorrelationId,
                CreatedUtc = interaction.CreatedUtc,
                EndedUtc = interaction.EndedUtc,
            });
    }
}
