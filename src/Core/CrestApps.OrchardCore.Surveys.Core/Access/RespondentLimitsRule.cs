using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Applies the respondent limits: total, per day, per week (from the site's first day of the week), per month and the
/// cooldown, counted from participation records. Accommodations add attempts. Limits are checked when a response starts
/// and again when it completes, under a lock, so two tabs never pass a limit.
/// </summary>
public sealed class RespondentLimitsRule : ISurveyAccessRule
{
    private readonly SurveyParticipationService _participation;
    private readonly SurveyResponseCounter _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RespondentLimitsRule"/> class.
    /// </summary>
    /// <param name="participation">The participation service.</param>
    /// <param name="counter">The response counter.</param>
    public RespondentLimitsRule(
        SurveyParticipationService participation,
        SurveyResponseCounter counter)
    {
        _participation = participation;
        _counter = counter;
    }

    /// <inheritdoc />
    public string Name => "RespondentLimits";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Limits;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
        => CheckAsync(context, completing: false);

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => CheckAsync(context, completing: true);

    private async Task<SurveyAccessDecision> CheckAsync(SurveyAccessContext context, bool completing)
    {
        var limits = context.Survey.Access?.Limits;

        if (context.IsTest || limits is null || string.IsNullOrEmpty(context.RespondentKey))
        {
            return SurveyAccessDecision.Allow;
        }

        if (limits.Total is null && limits.PerDay is null && limits.PerWeek is null && limits.PerMonth is null && limits.CooldownMinutes is null)
        {
            return SurveyAccessDecision.Allow;
        }

        // Counting started responses, the response being completed already counts; counting completed responses, it
        // does not yet, so it must still fit.
        var countStarted = limits.Counts == LimitCount.Started;

        if (completing && countStarted)
        {
            return SurveyAccessDecision.Allow;
        }

        var counts = await _participation.CountAsync(context.Survey, context.RespondentKey, context.Context, context.UtcNow);
        var used = countStarted ? counts.Started : counts.Completed;

        // The total never expires: participation records of a survey with a total limit are kept (upkeep deletes only
        // the others), so "once per person" holds for as long as the survey runs.
        var usedTotal = used.Total;

        // The total of an identified respondent counts the responses kept (started ones, or completed ones), so a
        // response staff delete gives the attempt back, as it does for the attempt number.
        var userId = context.Response?.UserId ?? context.Identity?.UserId;

        if (limits.Total is not null && context.Survey.Access?.Anonymity != AnonymityMode.Anonymous && !string.IsNullOrEmpty(userId))
        {
            usedTotal = countStarted
                ? await _counter.CountForUserAsync(context.Survey.ItemId, userId, context.Context?.ToString())
                : await _counter.CountCompletedForUserAsync(context.Survey.ItemId, userId, context.Context?.ToString());
        }

        if (limits.Total is { } total && usedTotal >= total + (context.Override?.ExtraAttempts ?? 0))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.LimitReached);
        }

        if ((limits.PerDay is { } perDay && used.Day >= perDay)
            || (limits.PerWeek is { } perWeek && used.Week >= perWeek)
            || (limits.PerMonth is { } perMonth && used.Month >= perMonth))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.LimitReached);
        }

        if (!completing && limits.CooldownMinutes is { } cooldown and > 0 && counts.LastCompletedUtc is { } last && context.UtcNow < last.AddMinutes(cooldown))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.CooldownActive);
        }

        return SurveyAccessDecision.Allow;
    }
}
