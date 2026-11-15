using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Indexes;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Reads what the course editors offer: the items a course contains and the other courses.
/// </summary>
public sealed class CourseEditorOptions
{
    private const int MaxCourses = 500;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly CourseLookup _courseLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseEditorOptions"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="courseLookup">The course lookup.</param>
    public CourseEditorOptions(
        ISession session,
        IContentManager contentManager,
        CourseLookup courseLookup)
    {
        _session = session;
        _contentManager = contentManager;
        _courseLookup = courseLookup;
    }

    /// <summary>
    /// Lists the latest versions of the items a course contains, in their list order.
    /// </summary>
    /// <param name="courseId">The content item id of the course.</param>
    /// <returns>The items.</returns>
    public async Task<IReadOnlyList<ContentItem>> GetContainedItemsAsync(string courseId)
    {
        if (string.IsNullOrEmpty(courseId))
        {
            return [];
        }

        var rows = await _session.QueryIndex<ContainedPartIndex>(index => index.ListContentItemId == courseId).ListAsync();
        var ids = rows.Select(row => row.ContentItemId).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = await _contentManager.GetAsync(ids, VersionOptions.Latest);

        return items.Where(item => item is not null && item.Has<CourseItemPart>()).OrderBy(item => item.DisplayText, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>
    /// Builds the options of a course item picker.
    /// </summary>
    /// <param name="courseId">The content item id of the course.</param>
    /// <param name="exceptId">An item to leave out, such as the one being edited.</param>
    /// <param name="emptyText">The text of the empty option.</param>
    /// <param name="selectedId">The selected item.</param>
    /// <returns>The options.</returns>
    public async Task<IList<SelectListItem>> GetItemOptionsAsync(string courseId, string exceptId, string emptyText, string selectedId)
    {
        var options = new List<SelectListItem> { new(emptyText, string.Empty) };

        foreach (var item in await GetContainedItemsAsync(courseId))
        {
            if (item.ContentItemId == exceptId)
            {
                continue;
            }

            options.Add(new SelectListItem(item.DisplayText, item.ContentItemId, item.ContentItemId == selectedId));
        }

        return options;
    }

    /// <summary>
    /// Builds the options of a course picker: the latest versions of every course, by title.
    /// </summary>
    /// <param name="exceptId">A course to leave out, such as the one being edited.</param>
    /// <param name="selectedIds">The selected courses.</param>
    /// <param name="onlyIds">When set, only these courses, such as the courses a person teaches.</param>
    /// <returns>The options.</returns>
    public async Task<IList<SelectListItem>> GetCourseOptionsAsync(string exceptId, IEnumerable<string> selectedIds, IReadOnlyCollection<string> onlyIds = null)
    {
        if (onlyIds is { Count: 0 })
        {
            return [];
        }

        var selected = new HashSet<string>(selectedIds ?? [], StringComparer.Ordinal);
        var courses = await _courseLookup.ListCoursesAsync(MaxCourses);

        return courses
            .Where(course => course.ContentItemId != exceptId && (onlyIds is null || onlyIds.Contains(course.ContentItemId)))
            .Select(course => new SelectListItem(course.DisplayText, course.ContentItemId, selected.Contains(course.ContentItemId)))
            .ToList();
    }
}
