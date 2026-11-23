using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Only an open survey and an open collector accept responses; a test response only needs a definition. The same goes
/// for completing a response: once staff close the survey or its link, a response in progress can't be submitted with
/// new answers. It is submitted for the respondent with what it had saved (<see cref="SurveyAccessContext.AutoSubmitted"/>).
/// A survey being deleted (<see cref="Survey.DeletingUtc"/>) accepts no new response at all.
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
        // A survey being deleted takes no response, not even a test one: its records are being removed.
        if (context.Survey?.DeletingUtc is not null)
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.NotFound));
        }

        if (context.IsTest)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        if (SurveyAvailability.IsClosedByStatus(context.Survey, collector: null))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.Closed));
        }

        if (context.Survey.Status != SurveyStatus.Open || context.Version is null)
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.NotOpen));
        }

        return Task.FromResult(SurveyAvailability.IsClosedByStatus(context.Survey, context.Collector)
            ? SurveyAccessDecision.Deny(SurveyErrorCode.Closed)
            : SurveyAccessDecision.Allow);
    }

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
    {
        if (context.IsTest || context.AutoSubmitted)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        return Task.FromResult(SurveyAvailability.IsClosedByStatus(context.Survey, context.Collector) || context.Survey.Status != SurveyStatus.Open
            ? SurveyAccessDecision.Deny(SurveyErrorCode.Closed)
            : SurveyAccessDecision.Allow);
    }
}
