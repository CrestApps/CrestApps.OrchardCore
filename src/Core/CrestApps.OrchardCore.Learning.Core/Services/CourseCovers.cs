using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Reads the cover picture of a course: the first file of the Cover media field of the Course type.
/// </summary>
public static class CourseCovers
{
    /// <summary>
    /// The name of the media field that holds the cover.
    /// </summary>
    public const string FieldName = "Cover";

    /// <summary>
    /// Gets the media path of a course's cover.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <returns>The path in the media library, or <see langword="null"/> without a cover.</returns>
    public static string GetPath(ContentItem course)
    {
        if (course is null)
        {
            return null;
        }

        var content = (JsonObject)course.Content;

        if (content[course.ContentType] is not JsonObject part ||
            part[FieldName]?["Paths"] is not JsonArray paths)
        {
            return null;
        }

        return paths.Select(path => path?.GetValueKind() == System.Text.Json.JsonValueKind.String ? path.GetValue<string>() : null)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }
}
