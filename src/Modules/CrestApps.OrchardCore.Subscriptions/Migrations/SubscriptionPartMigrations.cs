using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Defines the subscription content part metadata.
/// </summary>
public sealed class SubscriptionPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to define the subscription part.</param>
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

        return 1;
    }
}
