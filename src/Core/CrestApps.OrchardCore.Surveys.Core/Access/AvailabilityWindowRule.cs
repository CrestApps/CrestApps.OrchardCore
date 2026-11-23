using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses responses before the survey or collector opens and after it closes. An accommodation's window replaces the
/// survey's. A response in progress can't be submitted with new answers after the close either: it is submitted for the
/// respondent with what it had saved (<see cref="SurveyAccessContext.AutoSubmitted"/>).
/// </summary>
public sealed class AvailabilityWindowRule : ISurveyAccessRule
{
    /// <inheritdoc />
    public string Name => "AvailabilityWindow";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Window;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        var now = context.UtcNow;
        var opens = context.Override?.OpensUtc ?? context.Survey.Access?.OpensUtc;

        if ((opens is not null && now < opens) || (context.Collector?.OpensUtc is { } collectorOpens && now < collectorOpens))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.NotOpen));
        }

        return Task.FromResult(IsPastClose(context)
            ? SurveyAccessDecision.Deny(SurveyErrorCode.Closed)
            : SurveyAccessDecision.Allow);
    }

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => Task.FromResult(!context.IsTest && !context.AutoSubmitted && IsPastClose(context)
            ? SurveyAccessDecision.Deny(SurveyErrorCode.Closed)
            : SurveyAccessDecision.Allow);

    private static bool IsPastClose(SurveyAccessContext context)
        => SurveyAvailability.ClosesUtc(context.Survey, context.Override, context.Collector) is { } closes && context.UtcNow >= closes;
}
