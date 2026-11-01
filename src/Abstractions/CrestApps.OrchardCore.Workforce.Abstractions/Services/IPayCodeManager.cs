using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages pay codes.
/// </summary>
public interface IPayCodeManager : ICatalogManager<PayCode>
{
    /// <summary>
    /// Lists the ones in use, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<PayCode>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
