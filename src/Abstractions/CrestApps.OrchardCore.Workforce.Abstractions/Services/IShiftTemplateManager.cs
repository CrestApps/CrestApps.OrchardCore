using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages shift templates.
/// </summary>
public interface IShiftTemplateManager : ICatalogManager<ShiftTemplate>
{
    /// <summary>
    /// Lists the enabled templates a team can use, by name.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The templates.</returns>
    Task<IReadOnlyList<ShiftTemplate>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default);
}
