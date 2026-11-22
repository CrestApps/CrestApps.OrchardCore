namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Decides whether a respondent may start, resume or complete a response. Rules run in order; the first refusal wins. Register an
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
    /// Decides whether a respondent may go back to the response they left in progress. Limits, quotas and windows are
    /// not checked again here, so most rules allow it; a rule about who the respondent is (such as an enrollment)
    /// checks it again, because that may have changed since the response started.
    /// </summary>
    /// <param name="context">The access context, with the response in <see cref="SurveyAccessContext.Response"/>.</param>
    /// <returns>The decision.</returns>
    Task<SurveyAccessDecision> CheckResumeAsync(SurveyAccessContext context)
        => Task.FromResult(SurveyAccessDecision.Allow);

    /// <summary>
    /// Decides whether a response may complete (limits are checked again under a lock).
    /// </summary>
    /// <param name="context">The access context.</param>
    /// <returns>The decision.</returns>
    Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => Task.FromResult(SurveyAccessDecision.Allow);
}
