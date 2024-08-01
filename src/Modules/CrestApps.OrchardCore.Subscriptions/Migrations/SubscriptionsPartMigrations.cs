using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

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
            .WithDescription("Provides the key properties for any subscription.")
        );

        return 1;
    }
}
