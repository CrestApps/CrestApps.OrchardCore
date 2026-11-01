using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages time-off types.
/// </summary>
public interface ITimeOffTypeManager : ICatalogManager<TimeOffType>
{
    /// <summary>
    /// Lists the enabled types, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The types.</returns>
    Task<IReadOnlyList<TimeOffType>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
