using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class SubscriptionPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // The part is system-defined and injected automatically into any content type that uses the
        // Subscription stereotype (see SubscriptionPartContentTypeDefinitionHandler), so it is not
        // attachable manually.
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionPart", part => part
            .WithDisplayName("Subscription")
            .WithDescription("Provides the key properties for any subscription.")
        );

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        // Previously the part was attachable and had to be added manually. It is now system-defined and
        // injected automatically, so remove it from the attachable parts list.
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionPart", part => part
            .WithDisplayName("Subscription")
            .WithDescription("Provides the key properties for any subscription.")
        );

        return 2;
    }
}
