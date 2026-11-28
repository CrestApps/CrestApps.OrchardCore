using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// The pool counter used until even presentation is available: it keeps the shuffled order.
/// </summary>
public sealed class NullSurveyPoolCounter : ISurveyPoolCounter
{
    /// <inheritdoc />
    public IReadOnlyList<SurveyQuestion> Order(string surveyId, QuestionPool pool, IReadOnlyList<SurveyQuestion> candidates)
        => candidates;

    /// <inheritdoc />
    public Task RecordAsync(string surveyId, IReadOnlyCollection<string> questionIds, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
