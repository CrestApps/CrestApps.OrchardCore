using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal class CustomChatMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CustomChatMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("CustomChatPart", part => part
            .Attachable()
            .WithDisplayName("Artificial Intelligence Custom Chat")
            .WithDescription("Provides a way to add a Artificial Intelligence Custom Chat to a content item.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("CustomChat", type => type
            .WithPart("CustomChatPart")
            .DisplayedAs("Artificial Intelligence Custom Chat")
            .Draftable(false)
            .Listable(false)
            .Securable(false)
            .Creatable(false)
            .Versionable(false)
            .WithDescription("A widget to add a Artificial Intelligence Custom Chat.")
            .Stereotype("Widget")
        );

        return 1;
    }
}
