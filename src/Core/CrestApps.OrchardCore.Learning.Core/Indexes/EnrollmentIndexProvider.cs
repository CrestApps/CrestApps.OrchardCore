using CrestApps.OrchardCore.Learning.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Maps enrollments to their index rows and their grants.
/// </summary>
public sealed class EnrollmentIndexProvider : IndexProvider<Enrollment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentIndexProvider"/> class.
    /// </summary>
    public EnrollmentIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Enrollment> context)
    {
        context.For<EnrollmentIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(enrollment => new EnrollmentIndex
            {
                ItemId = enrollment.ItemId,
                UserId = enrollment.UserId,
                CourseId = enrollment.CourseId,
                OfferingId = string.IsNullOrEmpty(enrollment.OfferingId) ? null : enrollment.OfferingId,
                Status = enrollment.Status,
                IsOpen = enrollment.IsOpen,
                NeedsReview = enrollment.NeedsReview,
                Number = enrollment.Number,
                Cycle = enrollment.Cycle,
                CreatedUtc = enrollment.CreatedUtc,
                AccessEndsUtc = enrollment.AccessEndsUtc,
                DueUtc = enrollment.DueUtc,
                CompletedUtc = enrollment.CompletedUtc,
                LastActivityUtc = enrollment.LastActivityUtc,
                ProgressPercent = enrollment.ProgressPercent,
            });

        context.For<EnrollmentGrantIndex>()
            .ExclusiveToDocumentType()
            .Map(enrollment => (enrollment.Grants ?? [])
                .Where(grant => !string.IsNullOrEmpty(grant?.Source))
                .Select(grant => new EnrollmentGrantIndex
                {
                    EnrollmentId = enrollment.ItemId,
                    Source = grant.Source,
                    SourceId = grant.SourceId,
                }));
    }
}
