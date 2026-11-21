using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts the Survey quota reached event; the quota counter calls it when a quota fills.
/// </summary>
internal sealed class SurveyQuotaWorkflowEvents
{
    private readonly ISurveyWorkflowTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuotaWorkflowEvents"/> class.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    public SurveyQuotaWorkflowEvents(ISurveyWorkflowTrigger trigger)
    {
        _trigger = trigger;
    }

    /// <summary>
    /// Starts the event for a quota that reached its limit.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="quota">The quota.</param>
    /// <param name="count">The count reached.</param>
    public void QuotaReached(Survey survey, SurveyQuota quota, int count)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(quota);

        var input = SurveyWorkflowInputBuilder.ForSurvey(survey);

        input[SurveyWorkflowInput.QuotaName] = quota.Name;
        input["QuotaLimit"] = quota.Limit;
        input["QuotaCount"] = count;
        _trigger.Trigger(nameof(SurveyQuotaReachedEvent), input, survey.ItemId);
    }
}
