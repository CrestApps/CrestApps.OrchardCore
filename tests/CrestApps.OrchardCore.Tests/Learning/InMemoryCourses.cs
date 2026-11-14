using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Courses and course items held in memory, standing in for the content manager in Learning's service tests.
/// </summary>
internal sealed class InMemoryCourses : ICourseStructureService
{
    private readonly Dictionary<string, ContentItem> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _courseOfItem = new(StringComparer.Ordinal);

    public ContentItem AddCourse(string id, Action<CoursePart> configure = null, string title = null)
    {
        var course = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = LearningConstants.ContentTypes.Course,
            DisplayText = title ?? id,
            Published = true,
            Latest = true,
        };

        course.Alter<CoursePart>(part => configure?.Invoke(part));
        course.Alter<CourseOutlinePart>(part => part.Sections = [new CourseSection { Id = "s1", Title = "Section 1" }]);
        _items[id] = course;

        return course;
    }

    public ContentItem AddItem(string courseId, string id, string kind = LearningConstants.CourseItemKinds.Page, string completionMode = CompletionModes.Viewed, bool required = true, Action<CourseItemPart> configure = null, string sectionId = "s1")
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = LearningConstants.ContentTypes.PageLesson,
            DisplayText = "Item " + id,
            Published = true,
            Latest = true,
        };

        item.Alter<CourseItemPart>(part =>
        {
            part.Kind = kind;
            part.CompletionMode = completionMode;
            configure?.Invoke(part);
        });

        _items[id] = item;
        _courseOfItem[id] = courseId;

        if (sectionId is null)
        {
            return item;
        }

        var course = _items[courseId];

        course.Alter<CourseOutlinePart>(outline =>
        {
            var section = outline.Sections.FirstOrDefault(candidate => candidate.Id == sectionId);

            if (section is null)
            {
                section = new CourseSection { Id = sectionId, Title = sectionId };
                outline.Sections.Add(section);
            }

            section.Items.Add(new CourseOutlineItem { ContentItemId = id, Required = required });
        });

        return item;
    }

    public ContentItem AddOrphan(string id, Action<CourseItemPart> configure = null)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = LearningConstants.ContentTypes.PageLesson,
            DisplayText = "Item " + id,
            Published = true,
            Latest = true,
        };

        item.Alter<CourseItemPart>(part => configure?.Invoke(part));
        _items[id] = item;

        return item;
    }

    public ContentItem Get(string id)
        => _items[id];

    public Task<ContentItem> GetCourseAsync(string courseId)
        => Task.FromResult(courseId is not null && _items.TryGetValue(courseId, out var item) && item.Has<CoursePart>() ? item : null);

    public string GetCourseId(ContentItem item)
        => item is not null && _courseOfItem.TryGetValue(item.ContentItemId, out var courseId) ? courseId : null;

    public Task<CourseOutline> GetOutlineAsync(ContentItem course)
    {
        var outline = new CourseOutline { Course = course };

        foreach (var section in course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections)
        {
            foreach (var entry in section.Items)
            {
                if (_items.TryGetValue(entry.ContentItemId, out var item))
                {
                    outline.Entries.Add(new CourseOutlineEntry { Section = section, Item = item, Part = item.Get<CourseItemPart>(nameof(CourseItemPart)), Required = entry.Required });
                }
            }
        }

        return Task.FromResult(outline);
    }

    public Task<IReadOnlyCollection<string>> GetStaffCourseIdsAsync(string userId, params string[] roles)
    {
        IReadOnlyCollection<string> ids = _items.Values
            .Where(item => item.Has<CoursePart>() && item.Get<CoursePart>(nameof(CoursePart)).Staff.Any(staff => staff.UserId == userId && (roles.Length == 0 || roles.Contains(staff.Role))))
            .Select(item => item.ContentItemId)
            .ToArray();

        return Task.FromResult(ids);
    }

    public async Task<bool> IsCourseStaffAsync(string userId, string courseId, params string[] roles)
        => (await GetStaffCourseIdsAsync(userId, roles)).Contains(courseId);
}
