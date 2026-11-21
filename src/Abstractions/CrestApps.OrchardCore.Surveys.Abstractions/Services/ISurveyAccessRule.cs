namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Decides whether a respondent may start or complete a response. Rules run in order; the first refusal wins. Register an
/// implementation with <c>services.AddSurveyAccessRule&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyAccessRule
{
    /// <summary>
    /// Gets the unique name of the rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the order the rule runs in; lower first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Decides whether a response may start.
    /// </summary>
    /// <param name="context">The access context.</param>
    /// <returns>The decision.</returns>
    Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context);

    /// <summary>
    /// Decides whether a response may complete (limits are checked again under a lock).
    /// </summary>
    /// <param name="context">The access context.</param>
    /// <returns>The decision.</returns>
    Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => Task.FromResult(SurveyAccessDecision.Allow);
}
