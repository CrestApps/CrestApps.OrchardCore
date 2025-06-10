using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionSessionDisplayDriver : DisplayDriver<SubscriptionSession>
{
    public override Task<IDisplayResult> DisplayAsync(SubscriptionSession subscription, BuildDisplayContext context)
    {
        return CombineAsync(
            Shape("SubscriptionsMeta_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "Meta:20"),
            Shape("SubscriptionsActions_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "Actions:5"),
            Shape("SubscriptionsButtonActions_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(SubscriptionSession subscription, BuildEditorContext context)
    {
        return Initialize<SubscriptionsMetadata>("SubscriptionsMetadata_Edit", model =>
        {
            var metadata = subscription.As<SubscriptionsMetadata>();

            model.Subscriptions = metadata.Subscriptions;

        }).Location("Content:5");
    }
}
