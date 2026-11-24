using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Learning.Shapes;

/// <summary>
/// Keeps the body, the files, the video and the link of a course item out of its Summary display. Pages that list
/// content in Summary, such as search results, do not ask whether the reader may view each item, so a lesson shows
/// only its title there; the player shows the rest to people who may open it.
/// </summary>
/// <remarks>
/// It applies to every content type with the course item part, including types a site adds, which a placement file
/// keyed by content type could not cover.
/// </remarks>
internal sealed class CourseItemSummaryShapes : ShapeTableProvider
{
    private static readonly string[] _hiddenShapes = ["HtmlBodyPart", "MarkdownBodyPart", "MediaField", "YoutubeField", "LinkField"];

    private static readonly PlacementInfo _hidden = new("-");

    /// <inheritdoc />
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        foreach (var shapeType in _hiddenShapes)
        {
            builder.Describe(shapeType).Placement(IsCourseItemSummary, _hidden);
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsCourseItemSummary(ShapePlacementContext context)
        => context.DisplayType == "Summary"
            && context.ZoneShape is IShape shape
            && shape.TryGetProperty("ContentItem", out ContentItem contentItem)
            && contentItem?.Has(nameof(CourseItemPart)) == true;
}
