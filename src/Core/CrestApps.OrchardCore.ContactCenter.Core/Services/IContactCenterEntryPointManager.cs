using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for inbound entry points.
/// </summary>
public interface IContactCenterEntryPointManager : ICatalogManager<ContactCenterEntryPoint>
{
    /// <summary>
    /// Finds the entry point with the specified unique name.
    /// </summary>
    /// <param name="name">The entry point name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching entry point, or <see langword="null"/> when none exists.</returns>
    Task<ContactCenterEntryPoint> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled entry point.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled entry points.</returns>
    Task<IReadOnlyCollection<ContactCenterEntryPoint>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
