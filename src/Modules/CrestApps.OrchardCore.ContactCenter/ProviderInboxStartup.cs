using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Brings the provider webhook inbox under Contact Center retention when both are on: completed and dead-lettered
/// deliveries are purged by the Contact Center retention policy, and the inbox keeps completed deliveries for the
/// Contact Center's configured window.
/// </summary>
/// <remarks>
/// The inbox itself is the neutral <b>Provider Webhook Inbox</b> feature of the Webhooks module, which voice and SMS
/// depend on; it no longer needs Contact Center.
/// </remarks>
[RequireFeatures(WebhooksConstants.Feature.ProviderInbox)]
public sealed class ProviderInboxStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ProviderWebhookInboxOptions>()
            .Configure<IOptions<ContactCenterRetentionOptions>>((options, retention) => options.RetentionDays = retention.Value.WebhookInboxMessageRetentionDays);

        services.AddScoped<IContactCenterRetentionPolicy, ProviderWebhookInboxMessageRetentionPolicy>();
    }
}
