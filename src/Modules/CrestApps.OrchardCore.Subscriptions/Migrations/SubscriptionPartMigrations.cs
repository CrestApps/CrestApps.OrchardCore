using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Defines and updates the subscription content part metadata.
/// </summary>
public sealed class SubscriptionPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to update subscription part definitions.</param>
    public SubscriptionPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the system-defined subscription part definition.
    /// </summary>
    /// <returns>The next migration version number.</returns>
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

    /// <summary>
    /// Updates the subscription part definition after it changed from attachable to system-defined.
    /// </summary>
    /// <returns>The next migration version number.</returns>
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
