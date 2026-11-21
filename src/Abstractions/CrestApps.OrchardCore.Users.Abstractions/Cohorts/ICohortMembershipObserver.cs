namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Hears that people joined or left a cohort, after the change is saved, such as Learning, which enrolls the people of
/// the cohorts a course is linked to. Register one with <c>services.AddCohortMembershipObserver&lt;T&gt;()</c>.
/// </summary>
public interface ICohortMembershipObserver
{
    /// <summary>
    /// Called after people joined a cohort.
    /// </summary>
    /// <param name="cohort">The cohort.</param>
    /// <param name="userIds">The people who joined now; never the ones already in it.</param>
    /// <param name="actor">Who added them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task MembersAddedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after people left a cohort.
    /// </summary>
    /// <param name="cohort">The cohort.</param>
    /// <param name="userIds">The people who left now.</param>
    /// <param name="actor">Who removed them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task MembersRemovedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tells what uses a cohort, so a cohort something depends on is not deleted from under it. Register one with
/// <c>services.AddCohortUsage&lt;T&gt;()</c>.
/// </summary>
public interface ICohortUsage
{
    /// <summary>
    /// Lists what uses a cohort, by names an administrator recognizes, such as the courses linked to it.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The names of what uses it; empty when nothing does.</returns>
    Task<IReadOnlyList<string>> ListUsesAsync(string cohortId, CancellationToken cancellationToken = default);
}
