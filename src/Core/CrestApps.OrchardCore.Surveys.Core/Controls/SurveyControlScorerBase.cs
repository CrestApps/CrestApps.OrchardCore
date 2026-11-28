using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// A scorer with the common feedback: right or wrong from the score, the right answer text, and the feedback texts of
/// the key.
/// </summary>
public abstract class SurveyControlScorerBase : ISurveyControlScorer
{
    /// <inheritdoc />
    public abstract string ControlType { get; }

    /// <inheritdoc />
    public virtual bool AnswerKeyIsOrder => false;

    /// <inheritdoc />
    public abstract SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context);

    /// <inheritdoc />
    public virtual decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context)
        => context?.PointsOf(question) ?? question?.Scoring?.Points ?? 1;

    /// <inheritdoc />
    public virtual SurveyAnswerFeedback GetFeedback(SurveyQuestion question, SurveyAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(question);

        var scoring = question.Scoring ?? new SurveyQuestionScoring();
        var result = Score(question, answer, new SurveyScoringContext());
        var feedback = new SurveyAnswerFeedback
        {
            Correct = result.Points is null ? null : result.Correct,
            RightAnswer = RightAnswer(question),
            CorrectChoiceIds = scoring.CorrectChoiceIds?.ToList() ?? [],
            GeneralFeedback = scoring.GeneralFeedback,
        };

        feedback.Feedback = feedback.Correct switch
        {
            true => scoring.CorrectFeedback,
            false => scoring.IncorrectFeedback,
            _ => null,
        };

        foreach (var id in answer?.Values ?? [])
        {
            if (scoring.ChoiceFeedback is not null && scoring.ChoiceFeedback.TryGetValue(id, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                feedback.ChoiceFeedback[id] = text;
            }
        }

        return feedback;
    }

    /// <summary>
    /// Gets the right answer as text.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    protected virtual string RightAnswer(SurveyQuestion question)
        => null;

    /// <summary>
    /// Gets the penalty of one wrong choice when negative marking is on.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The scoring context.</param>
    /// <returns>The penalty, or 0.</returns>
    protected static decimal Penalty(SurveyQuestion question, SurveyScoringContext context)
        => context?.Settings?.NegativeMarking == true ? Math.Abs(question?.Scoring?.PenaltyPerWrong ?? 0) : 0;

    /// <summary>
    /// Gets the texts of choices, joined.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="ids">The choice ids.</param>
    /// <returns>The texts.</returns>
    protected static string ChoiceTexts(SurveyQuestion question, IEnumerable<string> ids)
        => string.Join(", ", (ids ?? []).Select(id => question?.Choices?.FirstOrDefault(choice => choice?.Id == id)?.Text ?? id));
}
