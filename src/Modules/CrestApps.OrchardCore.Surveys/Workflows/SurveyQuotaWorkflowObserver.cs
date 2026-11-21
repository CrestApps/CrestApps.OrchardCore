using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Raises the Survey quota reached workflow event when a quota fills.
/// </summary>
internal sealed class SurveyQuotaWorkflowObserver : ISurveyQuotaObserver
{
    private readonly SurveyQuotaWorkflowEvents _events;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuotaWorkflowObserver"/> class.
    /// </summary>
    /// <param name="events">The quota workflow events.</param>
    public SurveyQuotaWorkflowObserver(SurveyQuotaWorkflowEvents events)
    {
        _events = events;
    }

    /// <inheritdoc />
    public Task QuotaReachedAsync(Survey survey, SurveyQuota quota, int count)
    {
        _events.QuotaReached(survey, quota, count);

        return Task.CompletedTask;
    }
}
