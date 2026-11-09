using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the AI Webhooks feature: it declares the AI events to the Webhooks feature, which owns the subscriptions,
/// the signing, the delivery outbox and the admin. It also moves the subscriptions and deliveries of the AI webhooks
/// that came before, keeps their old delivery log, and keeps every AI delivery out of the Webhooks 30-day purge, as
/// the AI delivery log was never purged.
/// </summary>
[Feature(AIConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWebhookEventProvider, AIWebhookEventProvider>());
        services.AddScoped<IAIConversationStartingHandler, AIWebhookConversationStartingHandler>();

        // The records of the AI webhooks that came before: kept readable to be moved, and kept.
        services
            .AddScoped<IAIWebhookSubscriptionStore, AIWebhookSubscriptionStore>()
            .AddScoped<IAIWebhookDeliveryStore, AIWebhookDeliveryStore>()
            .AddScoped<AIWebhookImporter>()
            .AddIndexProvider<AIWebhookSubscriptionIndexProvider>()
            .AddIndexProvider<AIWebhookDeliveryIndexProvider>()
            .AddDataMigration<AIWebhookIndexMigrations>();

        // AI deliveries, the ones moved from the old log and the new ones, are never purged by default.
        services.Configure<WebhookDeliveryRetentionOptions>(options => options.KeptEventNames.UnionWith(AIWebhookEvents.All));
    }
}
