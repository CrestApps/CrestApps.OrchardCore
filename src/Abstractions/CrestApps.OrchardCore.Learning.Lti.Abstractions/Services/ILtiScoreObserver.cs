namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// Hears when a tool's score is stored, after it is saved and after a fully graded score reached the learner's progress.
/// Registered with <c>AddLtiScoreObserver</c>; the External score received workflow event is one.
/// </summary>
public interface ILtiScoreObserver
{
    /// <summary>
    /// Called once a score is stored.
    /// </summary>
    /// <param name="context">The score, its line item, the tool and the learner.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task ScoreReceivedAsync(LtiScoreReceivedContext context);
}
