using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace OrchardCore.CrestApps.Subscriptions.Migrations;

public sealed class SubscriptionsPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionsPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionsPart", part => part
            .Attachable()
            .WithDisplayName("Subscriptions")
            .WithDescription("Provides the key properties from subscription.")
        );

        return 1;
    }
}
