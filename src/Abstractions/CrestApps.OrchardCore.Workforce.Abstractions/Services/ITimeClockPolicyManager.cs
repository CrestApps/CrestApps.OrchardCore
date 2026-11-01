using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages time clock policies.
/// </summary>
public interface ITimeClockPolicyManager : ICatalogManager<TimeClockPolicy>
{
    /// <summary>
    /// Lists the ones in use, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<TimeClockPolicy>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
