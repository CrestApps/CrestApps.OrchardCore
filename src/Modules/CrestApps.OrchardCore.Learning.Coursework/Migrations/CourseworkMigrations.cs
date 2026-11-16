using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Migrations;

/// <summary>
/// Creates the submissions table, the coursework and rubric parts, the coursework course item type and the rubric type,
/// and adds the coursework type to the course outlines. The types are defined in code: a recipe run inside a migration
/// would wait forever on SQLite for the write lock the migration holds.
/// </summary>
internal sealed class CourseworkMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    public CourseworkMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    public async Task<int> CreateAsync()
    {
        await CourseworkIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CourseworkPart), part => part
            .Attachable(false)
            .WithDisplayName("Coursework")
            .WithDescription("Makes a course item coursework: the due date, what the learner hands in, the file rules, resubmission, and the rubric or points it is graded against."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(RubricPart), part => part
            .Attachable(false)
            .WithDisplayName("Rubric")
            .WithDescription("The criteria of a rubric, each with levels worth a number of points."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(CourseworkConstants.ContentTypes.Coursework, type => type
            .WithDisplayName("Coursework")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg").WithDisplayName("Instructions"))
            .WithPart(nameof(CourseworkPart), part => part.WithPosition("3"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = CourseworkConstants.CourseworkKind,
                    DefaultCompletionMode = CompletionModes.Graded,
                    Gradable = true,
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(CourseworkConstants.ContentTypes.Rubric, type => type
            .WithDisplayName("Rubric")
            .WithDescription("Criteria with levels and points that graders score coursework against.")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(RubricPart), part => part.WithPosition("2")));

        await _courseListSynchronizer.SynchronizeAsync();

        return 2;
    }

    // New coursework completes when it is graded (review round 8, 7.1); items already made keep their own mode.
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync(CourseworkConstants.ContentTypes.Coursework, type => type
            .WithPart(nameof(CourseItemPart), part => part
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = CourseworkConstants.CourseworkKind,
                    DefaultCompletionMode = CompletionModes.Graded,
                    Gradable = true,
                })));

        return 2;
    }
}
