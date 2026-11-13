using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores true or false questions. The choices are always <c>true</c> then <c>false</c>.
/// </summary>
public sealed class TrueFalseScorer : Services.IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.TrueFalse;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<TrueFalseQuestionPart>(nameof(TrueFalseQuestionPart)) ?? new TrueFalseQuestionPart();

        item.Choices = [new AttemptChoice { Id = "true", Text = "True" }, new AttemptChoice { Id = "false", Text = "False" }];
        item.Key.ChoiceIds = [part.Answer ? "true" : "false"];
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        var chosen = item.Response?.ChoiceIds ?? [];

        return chosen.Count == 1 && item.Key.ChoiceIds.Contains(chosen[0], StringComparer.Ordinal) ? item.Points : 0;
    }
}
