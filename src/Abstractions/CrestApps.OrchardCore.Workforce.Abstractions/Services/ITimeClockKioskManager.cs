using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages time clock kiosks.
/// </summary>
public interface ITimeClockKioskManager : ICatalogManager<TimeClockKiosk>
{
    /// <summary>
    /// Lists the ones in use, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<TimeClockKiosk>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
