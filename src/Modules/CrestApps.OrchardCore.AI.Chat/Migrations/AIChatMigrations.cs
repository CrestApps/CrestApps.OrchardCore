using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

public sealed class AIChatMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AIChatMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("AIProfilePart", part => part
            .Attachable()
            .WithDisplayName("Artificial Intelligence Chat")
            .WithDescription("Provides a way to add a Artificial Intelligence Chat to a content item.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("AIChat", type => type
            .Draftable(false)
            .Listable(false)
            .Securable(false)
            .Creatable(false)
            .Versionable(false)
            .DisplayedAs("Artificial Intelligence Chat")
            .WithDescription("A widget to add a Artificial Intelligence Chat.")
            .WithPart("AIProfilePart")
            .Stereotype("Widget")
        );

        return 1;
    }
}
