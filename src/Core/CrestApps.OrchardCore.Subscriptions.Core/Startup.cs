using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Subscriptions.Core;

[RequireFeatures("OrchardCore.Workflows")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register workflow events
        services.AddActivity<SubscriptionActivatedEvent, SubscriptionActivatedEvent>();
        services.AddActivity<SubscriptionInitializedEvent, SubscriptionInitializedEvent>();
        services.AddActivity<SubscriptionCompletedEvent, SubscriptionCompletedEvent>();
        services.AddActivity<SubscriptionFailedEvent, SubscriptionFailedEvent>();
        services.AddActivity<SubscribedTenantFailedSetupEvent, SubscribedTenantFailedSetupEvent>();
        services.AddActivity<SubscribedTenantSetupSucceededEvent, SubscribedTenantSetupSucceededEvent>();

        // Register workflow handler
        services.AddScoped<ISubscriptionHandler, WorkflowSubscriptionHandler>();
    }
}
