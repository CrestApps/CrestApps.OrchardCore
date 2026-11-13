using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Leaves essays to a grader. An empty answer scores zero at once. The grader guidance is not copied into the attempt
/// item: it is read from the question by graders only.
/// </summary>
public sealed class EssayScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Essay;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<EssayQuestionPart>(nameof(EssayQuestionPart)) ?? new EssayQuestionPart();

        item.MinWords = Math.Max(part.MinWords, 0);
        item.MaxWords = Math.Max(part.MaxWords, 0);
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
        => string.IsNullOrWhiteSpace(item.Response?.Texts?.FirstOrDefault()) ? 0 : null;
}
