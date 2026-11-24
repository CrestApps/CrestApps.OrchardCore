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

        var (counts, used, usedTotal) = await CountUsedAsync(context, countStarted, limits.Total is not null);

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

    /// <summary>
    /// Gets how many attempts the total limit leaves a respondent, counted exactly as the limit is checked: started or
    /// completed responses as the limit counts them, plus the extra attempts of the respondent's accommodation.
    /// </summary>
    /// <param name="context">The access context of a start.</param>
    /// <returns>The attempts left, never below zero, or <see langword="null"/> when no total limit applies.</returns>
    public async Task<int?> GetAttemptsLeftAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var limits = context.Survey?.Access?.Limits;

        if (context.IsTest || limits?.Total is not { } total || string.IsNullOrEmpty(context.RespondentKey))
        {
            return null;
        }

        var (_, _, usedTotal) = await CountUsedAsync(context, limits.Counts == LimitCount.Started, hasTotalLimit: true);

        return Math.Max(total + (context.Override?.ExtraAttempts ?? 0) - usedTotal, 0);
    }

    private async Task<(SurveyParticipationCounts Counts, SurveyParticipationWindowCounts Used, int UsedTotal)> CountUsedAsync(SurveyAccessContext context, bool countStarted, bool hasTotalLimit)
    {
        var counts = await _participation.CountAsync(context.Survey, context.RespondentKey, context.Context, context.UtcNow);
        var used = countStarted ? counts.Started : counts.Completed;

        // The total never expires: participation records of a survey with a total limit are kept (upkeep deletes only
        // the others), so "once per person" holds for as long as the survey runs.
        var usedTotal = used.Total;

        // The total of an identified respondent also counts the responses kept (started ones, or completed ones), so
        // completions whose participation records went before the upgrade that kept them still count. A voided one stays
        // used, as it does in the participation count of guests and anonymous respondents (see
        // SurveyResponseCounter.CountAttemptsUsedByUserAsync). The larger of the two counts: retention anonymizes old
        // responses (they lose their user) but keeps the participation of a survey with a total limit, so "once per
        // person" holds after it. A response staff delete takes its participation back too, so a delete gives the
        // attempt back in both counts.
        var userId = context.Response?.UserId ?? context.Identity?.UserId;

        if (hasTotalLimit && context.Survey.Access?.Anonymity != AnonymityMode.Anonymous && !string.IsNullOrEmpty(userId))
        {
            usedTotal = Math.Max(usedTotal, await _counter.CountAttemptsUsedByUserAsync(context.Survey.ItemId, userId, context.Context?.ToString(), countStarted));
        }

        return (counts, used, usedTotal);
    }
}
