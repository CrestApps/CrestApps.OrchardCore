using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the <b>Contact Center Provider Webhook Inbox</b> feature: the durable store, the inbox itself, its
/// retention policy, the schema it lives in, and the background task that retries due deliveries.
/// </summary>
/// <remarks>
/// Provider webhook delivery is at-least-once and can arrive while a node is restarting, so a callback is
/// committed here before any state-changing handler runs and is then processed from storage. Voice solved that
/// first, but the guarantee is not voice-specific: inbound SMS needs exactly the same thing. The inbox therefore
/// lives in its own dependency-only feature that each ingesting channel depends on, rather than inside Voice,
/// where an SMS-only tenant could not reach it.
/// </remarks>
[Feature(ContactCenterConstants.Feature.ProviderInbox)]
public sealed class ProviderInboxStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services
            .AddScoped<IProviderWebhookInboxStore, ProviderWebhookInboxStore>()
            .AddScoped<IProviderWebhookInbox, ProviderWebhookInbox>()
            .AddScoped<IContactCenterRetentionPolicy, ProviderWebhookInboxMessageRetentionPolicy>();

        services
            .AddIndexProvider<ProviderWebhookInboxMessageIndexProvider>()
            .AddDataMigration<ProviderWebhookInboxMessageIndexMigrations>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ProviderWebhookInboxBackgroundTask>());
    }
}
