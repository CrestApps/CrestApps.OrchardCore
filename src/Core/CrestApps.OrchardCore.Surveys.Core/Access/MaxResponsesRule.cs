using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses responses once the survey or the collector has all the completed responses it accepts.
/// </summary>
public sealed class MaxResponsesRule : ISurveyAccessRule
{
    private readonly SurveyResponseCounter _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaxResponsesRule"/> class.
    /// </summary>
    /// <param name="counter">The response counter.</param>
    public MaxResponsesRule(SurveyResponseCounter counter)
    {
        _counter = counter;
    }

    /// <inheritdoc />
    public string Name => "MaxResponses";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.MaxResponses;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
        => CheckAsync(context);

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => CheckAsync(context);

    private async Task<SurveyAccessDecision> CheckAsync(SurveyAccessContext context)
    {
        if (context.IsTest)
        {
            return SurveyAccessDecision.Allow;
        }

        if (context.Survey.Access?.MaxResponses is { } max && await _counter.CountCompletedAsync(context.Survey.ItemId, null) >= max)
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.MaxResponsesReached);
        }

        if (context.Collector?.MaxResponses is { } collectorMax
            && !string.IsNullOrEmpty(context.Collector.ItemId)
            && await _counter.CountCompletedAsync(context.Survey.ItemId, context.Collector.ItemId) >= collectorMax)
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.MaxResponsesReached);
        }

        return SurveyAccessDecision.Allow;
    }
}
