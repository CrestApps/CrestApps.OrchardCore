using System.Globalization;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Lists a plan the way its merchant set it up.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A plan with no position on its product part keeps the order its stored plan sort gives it.</item>
/// <item>A plan that sells through the product prices part already carries the interval of its default price;
/// a plan priced on its product part takes its interval from the subscription part, so its card reads, for
/// example, "$29 / month".</item>
/// </list>
/// </remarks>
public sealed class SubscriptionProductSearchDocumentContributor : IProductSearchDocumentContributor
{
    public Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document)
    {
        if (!contentItem.TryGet<SubscriptionPart>(out var subscription))
        {
            return Task.CompletedTask;
        }

        // A plan saved before products had a position keeps the order its plan sort gave it.
        document.Position ??= subscription.Sort;

        if (!document.BillingInterval.HasValue && document.Price.HasValue)
        {
            document.BillingInterval = ToInterval(subscription.DurationType);
            document.BillingDuration = Math.Max(1, subscription.BillingDuration);
        }

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
