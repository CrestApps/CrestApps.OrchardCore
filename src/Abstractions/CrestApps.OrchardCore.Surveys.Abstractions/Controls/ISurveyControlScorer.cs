using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Scores the answers of one question type. Register an implementation with
/// <c>services.AddSurveyControlScorer&lt;TScorer&gt;()</c>.
/// </summary>
public interface ISurveyControlScorer
{
    /// <summary>
    /// Gets the name of the question type the scorer serves.
    /// </summary>
    string ControlType { get; }

    /// <summary>
    /// Scores an answer. A result without points means a grader must score it.
    /// </summary>
    /// <param name="question">The question, with its key.</param>
    /// <param name="answer">The answer, or <see langword="null"/> when unanswered.</param>
    /// <param name="context">The scoring context.</param>
    /// <returns>The result.</returns>
    SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, SurveyScoringContext context);

    /// <summary>
    /// Gets the most points a question gives.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The scoring context.</param>
    /// <returns>The points.</returns>
    decimal MaxPoints(SurveyQuestion question, SurveyScoringContext context);

    /// <summary>
    /// Gets the feedback of an answer: right or wrong, the right answer and the feedback texts.
    /// </summary>
    /// <param name="question">The question, with its key.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <returns>The feedback.</returns>
    SurveyAnswerFeedback GetFeedback(SurveyQuestion question, SurveyAnswer answer);

    /// <summary>
    /// Gets whether the answer key is an order of the choices, as in a ranking. The choices of such a type are never
    /// shown in their designed order outside a respondent's own shuffled plan: the runner shuffles them, and the API
    /// definition and the public results do not give the designed order away.
    /// </summary>
    bool AnswerKeyIsOrder => false;
}
