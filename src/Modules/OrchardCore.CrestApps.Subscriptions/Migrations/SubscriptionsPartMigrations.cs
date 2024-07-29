using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.CrestApps.Subscriptions.Core;
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
            .WithDisplayName(SubscriptionsConstants.Stereotype)
            .WithDescription("Provides the key properties for any subscription.")
        );

        return 1;
    }
}
