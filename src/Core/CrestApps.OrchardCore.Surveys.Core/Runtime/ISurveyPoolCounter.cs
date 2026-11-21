using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Counts how often each pool member was presented, so pools that present evenly draw the least-presented first.
/// </summary>
public interface ISurveyPoolCounter
{
    /// <summary>
    /// Orders the candidates of a pool that presents evenly, least presented first, before the draw takes the first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="candidates">The candidates, already shuffled.</param>
    /// <returns>The candidates in draw order.</returns>
    IReadOnlyList<SurveyQuestion> Order(string surveyId, QuestionPool pool, IReadOnlyList<SurveyQuestion> candidates);

    /// <summary>
    /// Records the questions a response presents.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="questionIds">The questions drawn from pools that present evenly.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RecordAsync(string surveyId, IReadOnlyCollection<string> questionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the presentation counts of a survey before its plan is drawn, so <see cref="Order"/> can read them.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task PrepareAsync(string surveyId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
