using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses responses before the survey or collector opens and after it closes. An accommodation's window replaces the
/// survey's. A response in progress can't be submitted with new answers after the close and its grace either: it is
/// submitted for the respondent with what it had saved (<see cref="SurveyAccessContext.AutoSubmitted"/>).
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
    /// <remarks>
    /// A submit gets the same grace past the closing date as past the deadline (<see cref="SurveyAvailability.GraceSeconds"/>),
    /// so a last page posted just before the close, and still in flight at it, is completed with its answers.
    /// </remarks>
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => Task.FromResult(!context.IsTest && !context.AutoSubmitted && IsPastClose(context, SurveyAvailability.GraceSeconds(context.Version?.Definition))
            ? SurveyAccessDecision.Deny(SurveyErrorCode.Closed)
            : SurveyAccessDecision.Allow);

    private static bool IsPastClose(SurveyAccessContext context, int graceSeconds = 0)
        => SurveyAvailability.ClosesUtc(context.Survey, context.Override, context.Collector) is { } closes && context.UtcNow >= closes.AddSeconds(graceSeconds);
}
