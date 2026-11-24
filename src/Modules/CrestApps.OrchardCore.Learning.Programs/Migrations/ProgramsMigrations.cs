using CrestApps.OrchardCore.Learning.Programs.Core.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Programs.Migrations;

/// <summary>
/// Creates the tables of Learning programs and the Learning Path content type. The type is defined in code: a recipe
/// run inside a migration would wait forever on SQLite for the write lock the migration holds.
/// </summary>
internal sealed class ProgramsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProgramsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await ProgramsIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(LearningPathPart), part => part
            .Attachable(false)
            .WithDisplayName("Learning Path")
            .WithDescription("The ordered course sets of a learning path."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(ProgramsConstants.LearningPathContentType, type => type
            .WithDisplayName("Learning Path")
            .WithDescription("Ordered sets of courses that open one after another.")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(nameof(LearningPathPart), part => part.WithPosition("1")));

        return 1;
    }
}
