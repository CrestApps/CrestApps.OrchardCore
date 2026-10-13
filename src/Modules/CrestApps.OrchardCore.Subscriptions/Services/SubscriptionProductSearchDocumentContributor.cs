using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Lists a plan at a recurring price. A plan that sells through the product prices part already carries the
/// interval of its default price; a plan priced on its product part takes its interval from the subscription part,
/// so its card reads, for example, "$29 / month".
/// </summary>
public sealed class SubscriptionProductSearchDocumentContributor : IProductSearchDocumentContributor
{
    public Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document)
    {
        if (document.BillingInterval.HasValue ||
            !document.Price.HasValue ||
            !contentItem.TryGet<SubscriptionPart>(out var subscription))
        {
            return Task.CompletedTask;
        }

        document.BillingInterval = ToInterval(subscription.DurationType);
        document.BillingDuration = Math.Max(1, subscription.BillingDuration);

        return Task.CompletedTask;
    }

    public static BillingInterval ToInterval(DurationType durationType)
        => durationType switch
        {
            DurationType.Year => BillingInterval.Year,
            DurationType.Week => BillingInterval.Week,
            DurationType.Day => BillingInterval.Day,
            _ => BillingInterval.Month,
        };
}
