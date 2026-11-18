using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Reports how much work of one kind each user holds. The module that keeps the work registers one with
/// <see cref="TeamsServiceCollectionExtensions.AddTeamMemberLoadSource{TSource}"/>, so team assignment can balance it.
/// </summary>
public interface ITeamMemberLoadSource
{
    /// <summary>
    /// Gets the kind of work this source counts, the value of <see cref="TeamAssignmentRequest.WorkKind"/>.
    /// </summary>
    string WorkKind { get; }

    /// <summary>
    /// Gets the load of users. Users with no work may be left out.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The load by user id.</returns>
    Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
