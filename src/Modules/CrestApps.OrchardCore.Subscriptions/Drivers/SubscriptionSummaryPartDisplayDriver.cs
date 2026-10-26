using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using YesSql;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Provides the display shape for the subscription summary content part.
/// </summary>
public sealed class SubscriptionSummaryPartDisplayDriver : ContentPartDisplayDriver<SubscriptionSummaryPart>
{
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IMemoryCache _memoryCache;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionSummaryPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query subscription indexes.</param>
    /// <param name="clock">The clock used to evaluate active subscriptions.</param>
    /// <param name="memoryCache">The cache that keeps the figures between visits to the dashboard.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SubscriptionSummaryPartDisplayDriver(
        ISession session,
        IClock clock,
        IMemoryCache memoryCache,
        IStringLocalizer<SubscriptionSummaryPartDisplayDriver> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        _memoryCache = memoryCache;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the subscription summary display by querying subscription counts and revenue, or by reusing the figures
    /// kept for the minutes the widget is set to.
    /// </summary>
    /// <param name="part">The subscription summary content part.</param>
    /// <param name="context">The content part display context.</param>
    /// <returns>The display result for the subscription summary part.</returns>
    public override IDisplayResult Display(SubscriptionSummaryPart part, BuildPartDisplayContext context)
    {
        return Initialize<SubscriptionSummaryViewModel>(GetDisplayShapeType(context), async model =>
        {
            var minutes = Math.Clamp(part.CacheMinutes, 0, 24 * 60);
            var key = $"CrestApps.SubscriptionSummary:{part.ContentItem.ContentItemId}:{part.ContentItem.ModifiedUtc?.Ticks}";

            if (minutes > 0 && _memoryCache.TryGetValue(key, out SubscriptionSummaryViewModel cached))
            {
                Copy(cached, model);

                return;
            }

            // Every figure is read from the durable agreement and the payment ledger. A checkout session
            // records what somebody was asked to pay, so counting sessions as subscriptions and their totals
            // as revenue would report every abandoned checkout as a paying customer.
            model.TotalSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(collection: SubscriptionConstants.SubscriptionCollectionName).CountAsync();
            model.PendingSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index => index.Status == SubscriptionStatus.Incomplete, SubscriptionConstants.SubscriptionCollectionName).CountAsync();
            model.CompletedSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index => index.Status != SubscriptionStatus.Incomplete, SubscriptionConstants.SubscriptionCollectionName).CountAsync();

            model.ActiveSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index =>
                index.Status == SubscriptionStatus.Active ||
                index.Status == SubscriptionStatus.Trialing ||
                index.Status == SubscriptionStatus.PastDue, SubscriptionConstants.SubscriptionCollectionName).CountAsync();

            var confirmed = await _session.QueryIndex<PaymentAttemptIndex>(index =>
                index.ReferenceType == SubscriptionCheckout.ReferenceType &&
                index.State == PaymentAttemptState.Succeeded).ListAsync();

            model.TotalRevenue = confirmed.Sum(attempt => attempt.ConfirmedAmount);

            if (minutes > 0)
            {
                var copy = new SubscriptionSummaryViewModel();

                Copy(model, copy);
                _memoryCache.Set(key, copy, TimeSpan.FromMinutes(minutes));
            }
        }).Location("Detail", "Content").Location("DetailAdmin", "Content");
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(SubscriptionSummaryPart part, BuildPartEditorContext context)
        => Initialize<SubscriptionSummaryEditViewModel>(GetEditorShapeType(context), model => model.CacheMinutes = part.CacheMinutes);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SubscriptionSummaryPart part, UpdatePartEditorContext context)
    {
        var model = new SubscriptionSummaryEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.CacheMinutes is < 0 or > 24 * 60)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CacheMinutes), S["Keep the figures between 0 (don't) and 1440 minutes (a day)."]);
        }

        part.CacheMinutes = Math.Clamp(model.CacheMinutes, 0, 24 * 60);

        return await EditAsync(part, context);
    }

    private static void Copy(SubscriptionSummaryViewModel from, SubscriptionSummaryViewModel to)
    {
        to.TotalSubscriptions = from.TotalSubscriptions;
        to.ActiveSubscriptions = from.ActiveSubscriptions;
        to.PendingSubscriptions = from.PendingSubscriptions;
        to.CompletedSubscriptions = from.CompletedSubscriptions;
        to.TotalRevenue = from.TotalRevenue;
    }
}
