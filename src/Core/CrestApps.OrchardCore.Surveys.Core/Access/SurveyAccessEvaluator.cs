using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Runs the access rules in order; the first refusal wins.
/// </summary>
public sealed class SurveyAccessEvaluator
{
    private readonly ISurveyAccessRule[] _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAccessEvaluator"/> class.
    /// </summary>
    /// <param name="rules">The rules.</param>
    public SurveyAccessEvaluator(IEnumerable<ISurveyAccessRule> rules)
    {
        _rules = rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Decides whether a response may start.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The decision.</returns>
    public async Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _rules)
        {
            var decision = await rule.CheckStartAsync(context);

            if (decision is { IsAllowed: false })
            {
                return decision;
            }
        }

        return SurveyAccessDecision.Allow;
    }

    /// <summary>
    /// Decides whether a response left in progress may be resumed.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The decision.</returns>
    public async Task<SurveyAccessDecision> CheckResumeAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _rules)
        {
            var decision = await rule.CheckResumeAsync(context);

            if (decision is { IsAllowed: false })
            {
                return decision;
            }
        }

        return SurveyAccessDecision.Allow;
    }

    /// <summary>
    /// Decides whether a response may complete.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The decision.</returns>
    public async Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _rules)
        {
            var decision = await rule.CheckCompleteAsync(context);

            if (decision is { IsAllowed: false })
            {
                return decision;
            }
        }

        return SurveyAccessDecision.Allow;
    }
}
