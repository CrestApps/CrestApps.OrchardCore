using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Registers the <b>Provider Webhook Inbox</b> feature: the durable store, the inbox itself, the schema it lives in,
/// and the background task that retries due deliveries.
/// </summary>
/// <remarks>
/// Provider webhook delivery is at-least-once and can arrive while a node is restarting, so a callback is committed
/// here before any state-changing handler runs and is then processed from storage. The inbox is a dependency-only
/// feature that every ingesting module (SMS, voice, payments) depends on.
/// </remarks>
[Feature(WebhooksConstants.Feature.ProviderInbox)]
public sealed class ProviderInboxStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddProviderWebhookInbox();

        services
            .AddIndexProvider<ProviderWebhookInboxMessageIndexProvider>()
            .AddDataMigration<ProviderWebhookInboxMessageIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ProviderWebhookInboxBackgroundTask>());
    }
}
