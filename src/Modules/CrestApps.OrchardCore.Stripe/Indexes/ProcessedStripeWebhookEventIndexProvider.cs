using CrestApps.OrchardCore.Stripe.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Stripe.Indexes;

public sealed class ProcessedStripeWebhookEventIndexProvider : IndexProvider<ProcessedStripeWebhookEvent>
{
    public override void Describe(DescribeContext<ProcessedStripeWebhookEvent> context)
    {
        context.For<ProcessedStripeWebhookEventIndex>()
            .Map(record => new ProcessedStripeWebhookEventIndex
            {
                EventId = record.EventId,
                ProcessedUtc = record.ProcessedUtc,
            });
    }
}
