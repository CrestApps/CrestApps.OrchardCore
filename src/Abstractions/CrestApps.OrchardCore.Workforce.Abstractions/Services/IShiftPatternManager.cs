using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages rotations.
/// </summary>
public interface IShiftPatternManager : ICatalogManager<ShiftPattern>
{
    /// <summary>
    /// Lists a team's enabled rotations.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The rotations.</returns>
    Task<IReadOnlyList<ShiftPattern>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default);
}
