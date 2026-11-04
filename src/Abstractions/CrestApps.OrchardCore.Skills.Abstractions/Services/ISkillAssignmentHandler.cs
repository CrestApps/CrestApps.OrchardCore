namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Reacts when the skills a person holds change, in the same request, so other modules can keep a copy in step
/// (Contact Center keeps the agent's routing skills this way). Also raised once a day for each person whose
/// assignment starts or ends that day.
/// </summary>
public interface ISkillAssignmentHandler
{
    /// <summary>
    /// Called after a person's skills changed and before the session commits.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task AssignmentsChangedAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken = default);
}
