using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Lists the courses of the site: the latest version of every content item whose type has the course part.
/// </summary>
public sealed class CourseLookup
{
    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseLookup"/> class.
    /// </summary>
    public CourseLookup(ISession session, IContentDefinitionManager contentDefinitionManager)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Lists the content types that are courses.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListCourseTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition?.Name == nameof(CoursePart)))
            .Select(type => type.Name)
            .ToArray();

    /// <summary>
    /// Lists courses by name, at most some of them.
    /// </summary>
    public async Task<IReadOnlyList<ContentItem>> ListCoursesAsync(int max, CancellationToken cancellationToken = default)
    {
        var courseTypes = await ListCourseTypesAsync();

        if (courseTypes.Count == 0)
        {
            return [];
        }

        var types = courseTypes.ToArray();

        return (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest)
            .OrderBy(index => index.DisplayText)
            .Take(max)
            .ListAsync(cancellationToken)).ToArray();
    }
}
