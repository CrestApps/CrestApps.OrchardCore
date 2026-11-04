using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages activity codes.
/// </summary>
public interface IActivityCodeManager : ICatalogManager<ActivityCode>
{
    /// <summary>
    /// Lists the enabled activity codes, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The codes.</returns>
    Task<IReadOnlyList<ActivityCode>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
