using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Keeps the list part of every course type in step with the course item types: a course may contain any content type
/// that carries the course item part. Learning and the modules that add course item kinds call it from their migrations,
/// so the course list never names them.
/// </summary>
public sealed class CourseListSynchronizer
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseListSynchronizer"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public CourseListSynchronizer(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Lists the course item types.
    /// </summary>
    /// <returns>The names of the content types that carry the course item part.</returns>
    public async Task<string[]> GetCourseItemTypesAsync()
    {
        return (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition?.Name == nameof(CourseItemPart)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Adds every course item type to the list part of every course type, keeping the types a site added by hand.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task SynchronizeAsync()
    {
        var itemTypes = await GetCourseItemTypesAsync();

        foreach (var courseType in (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Where(IsCourseType))
        {
            var list = courseType.Parts.FirstOrDefault(part => part.PartDefinition?.Name == nameof(ListPart));

            if (list is null)
            {
                continue;
            }

            var settings = list.GetSettings<ListPartSettings>();
            var contained = (settings.ContainedContentTypes ?? []).Union(itemTypes, StringComparer.Ordinal).ToArray();

            if (contained.Length == (settings.ContainedContentTypes?.Length ?? 0))
            {
                continue;
            }

            await _contentDefinitionManager.AlterTypeDefinitionAsync(courseType.Name, type => type
                .WithPart(list.Name, nameof(ListPart), part => part.MergeSettings<ListPartSettings>(listSettings => listSettings.ContainedContentTypes = contained)));
        }
    }

    private static bool IsCourseType(ContentTypeDefinition type)
        => type.Parts.Any(part => part.PartDefinition?.Name == nameof(CoursePart));
}
