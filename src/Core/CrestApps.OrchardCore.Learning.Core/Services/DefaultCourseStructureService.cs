using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Reads courses and their outlines through the content manager. Each course item is a content item contained in the
/// course; the outline only lists item ids, and an id whose item is not published in this course is left out. Outlines
/// are kept for the request, since the access handler, the player and the progress service all read them.
/// </summary>
public sealed class DefaultCourseStructureService : ICourseStructureService
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly CourseItemKindOptions _kinds;
    private readonly Dictionary<string, CourseOutline> _outlines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentItem> _courses = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCourseStructureService"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="kinds">The course item kinds, which tell the items shown in place.</param>
    public DefaultCourseStructureService(
        IContentManager contentManager,
        ISession session,
        IOptions<CourseItemKindOptions> kinds)
    {
        _contentManager = contentManager;
        _session = session;
        _kinds = kinds.Value;
    }

    /// <inheritdoc />
    public async Task<ContentItem> GetCourseAsync(string courseId)
    {
        if (string.IsNullOrEmpty(courseId))
        {
            return null;
        }

        if (_courses.TryGetValue(courseId, out var cached))
        {
            return cached;
        }

        var course = await _contentManager.GetAsync(courseId, VersionOptions.Published);

        if (course is null || !course.Has<CoursePart>())
        {
            course = null;
        }

        _courses[courseId] = course;

        return course;
    }

    /// <inheritdoc />
    public string GetCourseId(ContentItem item)
    {
        var containerId = item?.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId;

        return string.IsNullOrEmpty(containerId) ? null : containerId;
    }

    /// <inheritdoc />
    public async Task<CourseOutline> GetOutlineAsync(ContentItem course)
    {
        ArgumentNullException.ThrowIfNull(course);

        var key = course.ContentItemVersionId ?? course.ContentItemId;

        if (key is not null && _outlines.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var outline = new CourseOutline { Course = course };
        var sections = course.Get<CourseOutlinePart>(nameof(CourseOutlinePart))?.Sections ?? [];
        var ids = sections
            .SelectMany(section => section.Items ?? [])
            .Select(entry => entry?.ContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length > 0)
        {
            var items = (await _contentManager.GetAsync(ids, VersionOptions.Published))
                .Where(item => item is not null && item.Has<CourseItemPart>() && GetCourseId(item) == course.ContentItemId)
                .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
            var added = new HashSet<string>(StringComparer.Ordinal);

            foreach (var section in sections)
            {
                foreach (var entry in section.Items ?? [])
                {
                    if (entry?.ContentItemId is null || !added.Add(entry.ContentItemId) || !items.TryGetValue(entry.ContentItemId, out var item))
                    {
                        continue;
                    }

                    var part = item.Get<CourseItemPart>(nameof(CourseItemPart));

                    // An item shown in place, such as text, is never required, whatever the outline says.
                    var inline = _kinds.Find(part?.Kind)?.Inline == true;

                    outline.Entries.Add(new CourseOutlineEntry
                    {
                        Section = section,
                        Item = item,
                        Part = part,
                        Required = entry.Required && !inline,
                        Inline = inline,
                    });
                }
            }
        }

        if (key is not null)
        {
            _outlines[key] = outline;
        }

        return outline;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetStaffCourseIdsAsync(string userId, params string[] roles)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var query = _session.QueryIndex<CourseStaffIndex>(index => index.UserId == userId && index.Published);

        if (roles is { Length: > 0 })
        {
            query = query.Where(index => index.Role.IsIn(roles));
        }

        return (await query.ListAsync()).Select(index => index.CourseId).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> IsCourseStaffAsync(string userId, string courseId, params string[] roles)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
        {
            return false;
        }

        var query = _session.QueryIndex<CourseStaffIndex>(index => index.UserId == userId && index.CourseId == courseId && index.Published);

        if (roles is { Length: > 0 })
        {
            query = query.Where(index => index.Role.IsIn(roles));
        }

        return await query.CountAsync() > 0;
    }
}
