using CrestApps.OrchardCore.Stripe.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Stripe.Indexes;

/// <summary>
/// Maps processed Stripe webhook event records to the index used for duplicate detection.
/// </summary>
public sealed class ProcessedStripeWebhookEventIndexProvider : IndexProvider<ProcessedStripeWebhookEvent>
{
    /// <summary>
    /// Describes how processed Stripe webhook event records are indexed.
    /// </summary>
    /// <param name="context">The YesSql index description context.</param>
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
