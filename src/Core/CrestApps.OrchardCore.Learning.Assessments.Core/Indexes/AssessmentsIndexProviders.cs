using CrestApps.OrchardCore.Learning.Assessments.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// The index providers of assessments.
/// </summary>
public static class AssessmentsIndexProviders
{
    /// <summary>
    /// Gets every index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
        => [new AssessmentAttemptIndexProvider(), new AssessmentOverrideIndexProvider(), new GradeScaleIndexProvider()];
}

/// <summary>
/// Maps attempts and their items.
/// </summary>
public sealed class AssessmentAttemptIndexProvider : IndexProvider<AssessmentAttempt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentAttemptIndexProvider"/> class.
    /// </summary>
    public AssessmentAttemptIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<AssessmentAttempt> context)
    {
        context.For<AssessmentAttemptIndex>()
            .Map(attempt => new AssessmentAttemptIndex
            {
                ItemId = attempt.ItemId,
                UserId = attempt.UserId,
                QuizId = attempt.QuizId,
                CourseId = attempt.CourseId,
                EnrollmentId = attempt.EnrollmentId,
                Number = attempt.Number,
                Status = attempt.Status,
                NeedsGrading = attempt.Status == AttemptStatus.NeedsGrading,
                StartedUtc = attempt.StartedUtc,
                DeadlineUtc = attempt.DeadlineUtc,
                SubmittedUtc = attempt.SubmittedUtc,
                Percent = attempt.Percent,
                Passed = attempt.Passed,
            });

        context.For<AttemptItemIndex>()
            .Map(attempt => attempt.Status is AttemptStatus.Graded or AttemptStatus.NeedsGrading or AttemptStatus.Submitted
                ? (attempt.Items ?? []).Select(item => new AttemptItemIndex
                {
                    AttemptId = attempt.ItemId,
                    QuizId = attempt.QuizId,
                    QuestionId = item.QuestionId,
                    QuestionType = item.QuestionType,
                    Score = item.Score,
                    Points = item.Points,
                    AttemptPercent = attempt.Percent,
                    SubmittedUtc = attempt.SubmittedUtc,
                })
                : []);
    }
}

/// <summary>
/// Maps overrides.
/// </summary>
public sealed class AssessmentOverrideIndexProvider : IndexProvider<AssessmentOverride>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentOverrideIndexProvider"/> class.
    /// </summary>
    public AssessmentOverrideIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<AssessmentOverride> context)
        => context.For<AssessmentOverrideIndex>()
            .Map(entry => new AssessmentOverrideIndex
            {
                ItemId = entry.ItemId,
                UserId = entry.UserId,
                QuizId = entry.QuizId,
                CreatedUtc = entry.CreatedUtc,
            });
}

/// <summary>
/// Maps grade scales.
/// </summary>
public sealed class GradeScaleIndexProvider : IndexProvider<GradeScale>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GradeScaleIndexProvider"/> class.
    /// </summary>
    public GradeScaleIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<GradeScale> context)
        => context.For<GradeScaleIndex>()
            .Map(scale => new GradeScaleIndex
            {
                ItemId = scale.ItemId,
                Name = scale.Name,
            });
}
