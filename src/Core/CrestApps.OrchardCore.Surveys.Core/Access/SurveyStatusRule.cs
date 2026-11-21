using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Only an open survey and an open collector accept responses; a test response only needs a definition.
/// </summary>
public sealed class SurveyStatusRule : ISurveyAccessRule
{
    /// <inheritdoc />
    public string Name => "Status";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Status;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        if (context.Survey.Status == SurveyStatus.Closed || context.Survey.Status == SurveyStatus.Archived)
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.Closed));
        }

        if (context.Survey.Status != SurveyStatus.Open || context.Version is null)
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.NotOpen));
        }

        if (context.Collector is { Status: CollectorStatus.Closed })
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.Closed));
        }

        return Task.FromResult(SurveyAccessDecision.Allow);
    }
}
