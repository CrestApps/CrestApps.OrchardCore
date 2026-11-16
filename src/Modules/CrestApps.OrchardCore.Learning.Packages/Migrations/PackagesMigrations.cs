using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Migrations;

/// <summary>
/// Creates the runtime state table, the package part and the learning package course item type, and adds the type to
/// the course outlines. The type is defined in code: a recipe run inside a migration would wait forever on SQLite for
/// the write lock the migration holds.
/// </summary>
internal sealed class PackagesMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    public PackagesMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    public async Task<int> CreateAsync()
    {
        await PackagesIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(LearningPackagePart), part => part
            .Attachable(false)
            .WithDisplayName("Learning package")
            .WithDescription("Makes a course item a learning package: the imported SCORM 1.2 package and the SCO it launches."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(PackagesConstants.ContentTypes.Package, type => type
            .WithDisplayName("Learning package")
            .WithDescription("A SCORM 1.2 package learners run in the course player.")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg").WithDisplayName("Introduction"))
            .WithPart(nameof(LearningPackagePart), part => part.WithPosition("3"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = PackagesConstants.PackageKind,
                    DefaultCompletionMode = CompletionModes.PackageReported,
                })));

        await _courseListSynchronizer.SynchronizeAsync();

        return 1;
    }
}
