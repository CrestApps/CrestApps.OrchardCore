using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class StripePriceSyncHandler : FeatureEventHandler
{
    public override Task EnabledAsync(IFeatureInfo feature)
    {
        if (feature.Id != SubscriptionConstants.Features.Stripe)
        {
            return Task.CompletedTask;
        }

        return StripePriceSyncService.SyncAllPricesInBackground();
    }
}