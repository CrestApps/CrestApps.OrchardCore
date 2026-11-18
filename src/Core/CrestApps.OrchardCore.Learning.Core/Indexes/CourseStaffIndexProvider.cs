using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Maps the staff of each course version to index rows. Courses are content items in the default collection.
/// </summary>
public sealed class CourseStaffIndexProvider : IndexProvider<ContentItem>
{
    /// <inheritdoc />
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CourseStaffIndex>()
            .ExclusiveToDocumentType()
            .When(item => item.Has<CoursePart>())
            .Map(item => (item.Get<CoursePart>(nameof(CoursePart)).Staff ?? [])
                .Where(staff => !string.IsNullOrEmpty(staff?.UserId))
                .GroupBy(staff => (staff.UserId, staff.Role ?? string.Empty))
                .Select(group => new CourseStaffIndex
                {
                    CourseId = item.ContentItemId,
                    UserId = group.Key.UserId,
                    Role = string.IsNullOrEmpty(group.Key.Item2) ? null : group.Key.Item2,
                    Published = item.Published,
                    Latest = item.Latest,
                }));
    }
}
