using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses responses before the survey or collector opens and after it closes. An accommodation's window replaces the
/// survey's.
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
        var closes = context.Override?.ClosesUtc ?? context.Survey.Access?.ClosesUtc;

        if ((opens is not null && now < opens) || (context.Collector?.OpensUtc is { } collectorOpens && now < collectorOpens))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.NotOpen));
        }

        if ((closes is not null && now >= closes) || (context.Collector?.ClosesUtc is { } collectorCloses && now >= collectorCloses))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.Closed));
        }

        return Task.FromResult(SurveyAccessDecision.Allow);
    }
}
