using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Indexes enrollment keys by course.
/// </summary>
public sealed class CourseEnrollmentKeyIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }
}

/// <summary>
/// Maps enrollment keys.
/// </summary>
public sealed class CourseEnrollmentKeyIndexProvider : IndexProvider<CourseEnrollmentKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CourseEnrollmentKeyIndexProvider"/> class.
    /// </summary>
    public CourseEnrollmentKeyIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CourseEnrollmentKey> context)
        => context.For<CourseEnrollmentKeyIndex>()
            .Map(key => new CourseEnrollmentKeyIndex
            {
                ItemId = key.ItemId,
                CourseId = key.CourseId,
            });
}
