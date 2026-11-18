using CrestApps.OrchardCore.Learning.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Maps progress documents to their index row and to one row per course item.
/// </summary>
public sealed class EnrollmentProgressIndexProvider : IndexProvider<EnrollmentProgress>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentProgressIndexProvider"/> class.
    /// </summary>
    public EnrollmentProgressIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<EnrollmentProgress> context)
    {
        context.For<EnrollmentProgressIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(progress => new EnrollmentProgressIndex
            {
                ItemId = progress.ItemId,
                EnrollmentId = progress.EnrollmentId,
                UserId = progress.UserId,
                CourseId = progress.CourseId,
            });

        context.For<CourseItemProgressIndex>()
            .ExclusiveToDocumentType()
            .Map(progress => (progress.Items ?? [])
                .Where(item => !string.IsNullOrEmpty(item?.ContentItemId))
                .Select(item => new CourseItemProgressIndex
                {
                    EnrollmentId = progress.EnrollmentId,
                    UserId = progress.UserId,
                    CourseId = progress.CourseId,
                    ContentItemId = item.ContentItemId,
                    State = item.State,
                    LastAccessUtc = item.LastAccessUtc ?? item.FirstAccessUtc ?? item.CompletedUtc ?? DateTime.MinValue,
                    CompletedUtc = item.CompletedUtc,
                    SecondsSpent = item.SecondsSpent,
                    Score = item.Score,
                    SelfReported = item.SelfReported,
                }));
    }
}
