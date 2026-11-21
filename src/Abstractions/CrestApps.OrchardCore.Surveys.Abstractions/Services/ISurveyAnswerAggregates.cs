using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Aggregates computed in SQL over the answer index of completed, non-test responses.
/// </summary>
public interface ISurveyAnswerAggregates
{
    /// <summary>
    /// Counts the responses that match a filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<long> CountResponsesAsync(SurveyAnswerFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the responses that answered a question.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<long> CountAnsweredAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts how many times each choice of a question was chosen, per row (the row key is empty for questions
    /// without rows).
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Row id (or empty), then choice id, then count.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>> CountChoicesAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes statistics of the number values of a question.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="rowId">The row, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<SurveyNumberStatistics> NumberStatisticsAsync(SurveyAnswerFilter filter, string questionId, string rowId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a sample of the latest text answers of a question.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The texts.</returns>
    Task<IReadOnlyList<string>> TextSamplesAsync(SurveyAnswerFilter filter, string questionId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ids of the responses that chose a choice of a question, to segment results.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="choiceId">The choice.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response ids.</returns>
    Task<IReadOnlyCollection<string>> ResponsesWithChoiceAsync(SurveyAnswerFilter filter, string questionId, string choiceId, CancellationToken cancellationToken = default);
}
