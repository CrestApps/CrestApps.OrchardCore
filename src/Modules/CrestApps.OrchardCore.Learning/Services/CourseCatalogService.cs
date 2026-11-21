using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Taxonomies.Indexing;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Builds the course catalog: the published courses, by title, of a category and the categories under it, or of every
/// category, with words to find in the titles. The categories are the terms of the courses' category field
/// (<see cref="CourseCategoryService"/>); without one, the catalog lists every course.
/// </summary>
public sealed class CourseCatalogService
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseCategoryService _categories;
    private readonly IMediaFileStore _mediaFileStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCatalogService"/> class.
    /// </summary>
    public CourseCatalogService(
        ISession session,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        CourseCategoryService categories,
        IMediaFileStore mediaFileStore,
        IClock clock)
    {
        _session = session;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _categories = categories;
        _mediaFileStore = mediaFileStore;
        _clock = clock;
    }

    /// <summary>
    /// Builds a page of the catalog.
    /// </summary>
    /// <param name="categoryId">The category browsed, or <see langword="null"/> for every course.</param>
    /// <param name="search">Words to find in the course titles, or <see langword="null"/>.</param>
    /// <param name="pager">The page.</param>
    /// <returns>The catalog; its pager shape is left to the caller.</returns>
    public async Task<CourseCatalogViewModel> BuildAsync(string categoryId, string search, Pager pager)
    {
        ArgumentNullException.ThrowIfNull(pager);

        var tree = await _categories.GetTreeAsync();
        var category = tree.Find(categoryId);
        var model = new CourseCatalogViewModel
        {
            Category = category?.Id,
            Search = search?.Trim(),
            CategoryPath = category is null ? null : tree.GetPathText(category.Id),
            Categories = [.. tree.Nodes.Select(node => new CourseCatalogCategoryViewModel
            {
                Id = node.Id,
                Name = node.Name,
                Level = node.Level,
                Selected = node.Id == category?.Id,
            })],
        };

        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(CoursePart)))
            .Select(type => type.Name)
            .ToArray();

        if (types.Length == 0)
        {
            return model;
        }

        var query = _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(types));

        if (!string.IsNullOrEmpty(model.Search))
        {
            var words = model.Search;
            query = query.Where(index => index.DisplayText.Contains(words));
        }

        if (category is not null)
        {
            // A category lists its own courses and those of the categories under it.
            var termIds = CourseCategoryTerms.WithDescendants(tree, category.Id).ToArray();
            var courseIds = (await _session.QueryIndex<TaxonomyIndex>(index => index.Published && index.TermContentItemId.IsIn(termIds)).ListAsync())
                .Select(index => index.ContentItemId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (courseIds.Length == 0)
            {
                return model;
            }

            query = query.Where(index => index.ContentItemId.IsIn(courseIds));
        }

        model.Total = await query.CountAsync();

        var courses = await query
            .OrderBy(index => index.DisplayText)
            .Skip(pager.GetStartIndex())
            .Take(pager.PageSize)
            .ListAsync();
        var now = _clock.UtcNow;

        foreach (var course in courses)
        {
            var part = course.Get<CoursePart>(nameof(CoursePart));
            var row = new CourseCatalogRowViewModel
            {
                ContentItemId = course.ContentItemId,
                Title = course.DisplayText,
                Code = part?.Code,
                Summary = part?.Summary,
                StartsUtc = part?.StartsUtc,
                EndsUtc = part?.EndsUtc,
                Schedule = CourseSchedules.Of(part, now),
                CoverUrl = CourseCovers.GetPath(course) is { } cover ? _mediaFileStore.MapPathToPublicUrl(cover) : null,
                DisplayRouteValues = (await _contentManager.PopulateAspectAsync<ContentItemMetadata>(course)).DisplayRouteValues,
            };

            foreach (var id in await _categories.GetCategoryIdsAsync(course))
            {
                if (tree.GetPathText(id) is { } path)
                {
                    row.Categories.Add(path);
                }
            }

            model.Courses.Add(row);
        }

        return model;
    }
}
