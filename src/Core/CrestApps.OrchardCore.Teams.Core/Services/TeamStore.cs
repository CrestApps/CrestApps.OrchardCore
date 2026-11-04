using CrestApps.OrchardCore.Teams.Core.Indexes;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Teams.Core.Services;

/// <summary>
/// Stores teams, one document each, in the <see cref="TeamsConstants.CollectionName"/> collection. Its writes skip the
/// catalog handlers; write through the team manager when they must run.
/// </summary>
public sealed class TeamStore : DocumentCatalog<Team, TeamIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeamStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public TeamStore(ISession session)
        : base(session)
    {
        CollectionName = TeamsConstants.CollectionName;
    }

    /// <summary>
    /// Lists every team, ordered by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams.</returns>
    public Task<IReadOnlyList<Team>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }
}
