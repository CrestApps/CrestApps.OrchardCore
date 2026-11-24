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

    /// <summary>
    /// Finds courses whose title contains a text, by name, at most some of them: what a course picker searches, so a
    /// site with hundreds of courses never renders them all.
    /// </summary>
    /// <param name="text">The text to look for; empty for the first courses by name.</param>
    /// <param name="max">The most courses to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The latest versions of the courses found.</returns>
    public async Task<IReadOnlyList<ContentItem>> SearchCoursesAsync(string text, int max, CancellationToken cancellationToken = default)
    {
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return await ListCoursesAsync(max, cancellationToken);
        }

        var courseTypes = await ListCourseTypesAsync();

        if (courseTypes.Count == 0)
        {
            return [];
        }

        var types = courseTypes.ToArray();

        // A search inside titles can't seek, so it goes without the grouping (see AGENTS.md, YesSql index declarations).
        return (await _session.Query<ContentItem>()
            .NoIndexOptimizations()
            .With<ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest && index.DisplayText.Contains(text))
            .OrderBy(index => index.DisplayText)
            .Take(max)
            .ListAsync(cancellationToken)).ToArray();
    }
}
