using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="InteractionEvent"/> documents to the <see cref="InteractionEventIndex"/>.
/// </summary>
public sealed class InteractionEventIndexProvider : IndexProvider<InteractionEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventIndexProvider"/> class.
    /// </summary>
    public InteractionEventIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<InteractionEvent> context)
    {
        context
            .For<InteractionEventIndex>()
            .Map(interactionEvent => new InteractionEventIndex
            {
                ItemId = interactionEvent.ItemId,
                InteractionId = interactionEvent.InteractionId,
                EventType = interactionEvent.EventType,
                AggregateType = interactionEvent.AggregateType,
                AggregateId = interactionEvent.AggregateId,
                CorrelationId = interactionEvent.CorrelationId,
                IdempotencyKey = interactionEvent.IdempotencyKey,
                IdempotencyClaimKey = ContactCenterClaimKeys.BuildEventIdempotencyClaim(
                    interactionEvent.IdempotencyKey,
                    interactionEvent.ItemId),
                OccurredUtc = interactionEvent.OccurredUtc,
            });
    }
}
