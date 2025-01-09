using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

public sealed class WidgetsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public WidgetsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("OpenAIChatPart", part => part
            .Attachable()
            .WithDisplayName("OpenAI Chat")
            .WithDescription("Provides a way to add an OpenAI chat to a content item.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("OpenAIChat", type => type
            .Draftable(false)
            .Listable(false)
            .Securable(false)
            .Creatable(false)
            .Versionable(false)
            .DisplayedAs("OpenAI Chat")
            .WithDescription("Provides a way to add an OpenAI chat to a content item.")
            .WithPart("OpenAIChatPart")
            .Stereotype("Widget")
        );

        return 1;
    }
}
