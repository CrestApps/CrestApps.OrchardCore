using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Media.ViewModels;

namespace CrestApps.OrchardCore.Learning.Shapes;

/// <summary>
/// Shows the files of a course item to learners under their original names (the <c>MediaField-LearningFiles</c>
/// template) instead of the stored names Orchard Core Media gives attached files.
/// </summary>
/// <remarks>
/// It applies to every content type with the course item part, including types a site adds.
/// </remarks>
internal sealed class CourseItemFileShapes : ShapeTableProvider
{
    /// <summary>
    /// The alternate of a course item's media field.
    /// </summary>
    internal const string Alternate = "MediaField__LearningFiles";

    /// <inheritdoc />
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("MediaField").OnDisplaying(context =>
        {
            if (context.Shape.Metadata.DisplayType != "Summary"
                && context.Shape is DisplayMediaFieldViewModel model
                && model.Field?.ContentItem?.Has(nameof(CourseItemPart)) == true)
            {
                context.Shape.Metadata.Alternates.Add(Alternate);
            }
        });

        return ValueTask.CompletedTask;
    }
}
