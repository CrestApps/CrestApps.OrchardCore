namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Creates, renames and deletes cohorts and keeps their members. Every change of members tells the registered
/// <see cref="ICohortMembershipObserver"/>s, with only the people who actually joined or left.
/// </summary>
public interface ICohortService
{
    /// <summary>
    /// Pages the cohorts, by name.
    /// </summary>
    /// <param name="search">A part of a name to find, or <see langword="null"/> for every cohort.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many cohorts match, and the page.</returns>
    Task<(int Count, IReadOnlyList<Cohort> Items)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists cohorts by name, up to a limit, such as for a picker.
    /// </summary>
    /// <param name="take">The most to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cohorts.</returns>
    Task<IReadOnlyList<Cohort>> ListAsync(int take = 500, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a cohort by id.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cohort, or <see langword="null"/>.</returns>
    Task<Cohort> FindByIdAsync(string cohortId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a cohort by name, without regard to case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cohort, or <see langword="null"/>.</returns>
    Task<Cohort> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a cohort. A name another cohort has is refused.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="description">A description.</param>
    /// <param name="cohortId">The id to give it, such as one a recipe keeps; empty for a new one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<CohortResult> CreateAsync(string name, string description, string cohortId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a cohort or changes its description. A name another cohort has is refused.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<CohortResult> UpdateAsync(string cohortId, string name, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cohort and its members. A cohort something uses (an <see cref="ICohortUsage"/>) is refused, so the
    /// observers are not told: nothing that acts on its members depends on it any more.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Why it was refused; empty when it was deleted.</returns>
    Task<IReadOnlyList<string>> DeleteAsync(string cohortId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists what uses a cohort, by names an administrator recognizes.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The names; empty when nothing uses it.</returns>
    Task<IReadOnlyList<string>> ListUsesAsync(string cohortId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the members of a cohort, the most recently added first.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many members there are, and the page.</returns>
    Task<(int Count, IReadOnlyList<CohortMember> Items)> PageMembersAsync(string cohortId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the members of a cohort.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of members.</returns>
    Task<int> CountMembersAsync(string cohortId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the user ids of every member of a cohort.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyList<string>> ListMemberIdsAsync(string cohortId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the cohorts a user is in.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cohorts, by name.</returns>
    Task<IReadOnlyList<Cohort>> ListForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides whether a user is in a cohort.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the user is a member.</returns>
    Task<bool> IsMemberAsync(string cohortId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds people to a cohort. People already in it, and ids of no user, are left out.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="userIds">The user ids.</param>
    /// <param name="actor">Who adds them: a user id or the name of a process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids that joined now.</returns>
    Task<IReadOnlyList<string>> AddMembersAsync(string cohortId, IEnumerable<string> userIds, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes people from a cohort. People not in it are left out.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="userIds">The user ids.</param>
    /// <param name="actor">Who removes them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids that left now.</returns>
    Task<IReadOnlyList<string>> RemoveMembersAsync(string cohortId, IEnumerable<string> userIds, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from every cohort, such as when the user is deleted.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="actor">Who removes them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RemoveFromAllAsync(string userId, string actor, CancellationToken cancellationToken = default);
}
