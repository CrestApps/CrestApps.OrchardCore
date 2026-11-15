using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Classes.Migrations;

/// <summary>
/// Creates the class part and the class course item type, and adds the type to the course outlines.
/// </summary>
internal sealed class ClassesMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    public ClassesMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ClassPart), part => part
            .WithDisplayName("Class")
            .WithDescription("Makes a course item a class: the Scheduling appointment type and resources of its dated sessions, the defaults of new sessions and the minutes a partial attendance needs."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(ClassesConstants.ContentTypes.Class, type => type
            .WithDisplayName("Class")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(ClassPart), part => part.WithPosition("2"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = ClassesConstants.ClassKind,
                    DefaultCompletionMode = CompletionModes.Attended,
                })));

        await _courseListSynchronizer.SynchronizeAsync();

        return 1;
    }
}
