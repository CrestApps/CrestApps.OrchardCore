using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// The provider webhook inbox feature moved from Contact Center to the Webhooks module. Tenants that enabled it, and
/// the steps its migration already ran, must carry over unchanged.
/// </summary>
public sealed class ProviderInboxFeatureMoveTests
{
    [Fact]
    public void FeatureId_KeepsTheValueTenantsEnabled()
    {
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.ProviderInbox", WebhooksConstants.Feature.ProviderInbox);
        Assert.Equal(WebhooksConstants.Feature.ProviderInbox, ContactCenterConstants.Feature.ProviderInbox);
    }

    [Fact]
    public void Migration_KeepsTheFullNameOrchardRecordedItsStepsUnder()
    {
        // Orchard finds the steps a migration already ran by the migration's full type name. A new name would run
        // CreateAsync again on a tenant that already has the inbox table, and the activation would fail.
        Assert.Equal(
            "CrestApps.OrchardCore.ContactCenter.Migrations.ProviderWebhookInboxMessageIndexMigrations",
            typeof(ProviderWebhookInboxMessageIndexMigrations).FullName);
    }

    [Fact]
    public void InboxHosting_LivesInTheWebhooksModule_NotInContactCenter()
    {
        Assert.Equal("CrestApps.OrchardCore.Webhooks", typeof(ProviderWebhookInboxMessageIndexMigrations).Assembly.GetName().Name);
        Assert.Equal("CrestApps.OrchardCore.Webhooks", typeof(ProviderWebhookInboxMessageIndexProvider).Assembly.GetName().Name);
    }
}
