using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses new responses once a quota that counts every response is full and stops new respondents, and refuses to
/// complete a response whose quota closes the survey and is already full. Matching quotas with a rule screen out at
/// completion instead (the response service applies them).
/// </summary>
public sealed class QuotaRule : ISurveyAccessRule
{
    private readonly ISurveyQuotaService _quotas;
    private readonly SurveyLogicEngine _logic;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaRule"/> class.
    /// </summary>
    /// <param name="quotas">The quota service.</param>
    /// <param name="logic">The logic engine.</param>
    public QuotaRule(
        ISurveyQuotaService quotas,
        SurveyLogicEngine logic)
    {
        _quotas = quotas;
        _logic = logic;
    }

    /// <inheritdoc />
    public string Name => "Quota";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Quota;

    /// <inheritdoc />
    public async Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsTest || context.Version?.Definition is null)
        {
            return SurveyAccessDecision.Allow;
        }

        // A quota without a rule counts every response, so once it is full nobody can fill it.
        var unconditional = SurveyQuotaService.Active(context.Version.Definition)
            .Where(quota => quota.Action != QuotaAction.None && ConditionRule.IsEmpty(quota.Rule))
            .ToList();

        if (unconditional.Count == 0)
        {
            return SurveyAccessDecision.Allow;
        }

        var counts = await _quotas.GetCountsAsync(context.Survey.ItemId);

        return unconditional.Any(quota => (counts.TryGetValue(quota.Id, out var count) ? count : 0) >= quota.Limit)
            ? SurveyAccessDecision.Deny(SurveyErrorCode.QuotaFull)
            : SurveyAccessDecision.Allow;
    }

    /// <inheritdoc />
    public async Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsTest || context.Response is null || context.Version?.Definition is null)
        {
            return SurveyAccessDecision.Allow;
        }

        var closing = SurveyQuotaService.Active(context.Version.Definition).Where(quota => quota.Action == QuotaAction.CloseSurvey).ToList();

        if (closing.Count == 0)
        {
            return SurveyAccessDecision.Allow;
        }

        var counts = await _quotas.GetCountsAsync(context.Survey.ItemId);
        var state = _logic.Evaluate(context.Version.Definition, context.Response, SurveyRuntimeVersions.CacheKey(context.Version));

        return closing.Any(quota => (counts.TryGetValue(quota.Id, out var count) ? count : 0) >= quota.Limit
            && _logic.Holds(context.Version.Definition, context.Response, state, quota.Rule, null, SurveyRuntimeVersions.CacheKey(context.Version)))
            ? SurveyAccessDecision.Deny(SurveyErrorCode.QuotaFull)
            : SurveyAccessDecision.Allow;
    }
}
