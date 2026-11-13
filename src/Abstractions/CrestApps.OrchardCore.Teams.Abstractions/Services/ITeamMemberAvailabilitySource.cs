namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Says which users can't take new work now: off shift, on leave, out of office, signed out. A module that knows
/// registers one with <see cref="TeamsServiceCollectionExtensions.AddTeamMemberAvailabilitySource{TSource}"/>. With no
/// source registered, everybody is available.
/// </summary>
public interface ITeamMemberAvailabilitySource
{
    /// <summary>
    /// Gets the users, among those asked about, who are not available now.
    /// </summary>
    /// <param name="userIds">The users asked about.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The ids of the users who are not available.</returns>
    Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
