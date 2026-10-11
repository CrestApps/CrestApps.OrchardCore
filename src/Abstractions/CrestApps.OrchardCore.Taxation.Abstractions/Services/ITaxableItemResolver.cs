using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves taxable items from arbitrary source objects by delegating to the registered
/// <see cref="ITaxableItemProvider"/> instances.
/// </summary>
public interface ITaxableItemResolver
{
    /// <summary>
    /// Resolves a taxable item from the supplied source object.
    /// </summary>
    /// <param name="source">The source object to convert.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resolved taxable item, or <see langword="null"/> when no provider can convert the source.</returns>
    ValueTask<ITaxableItem> ResolveAsync(object source, CancellationToken cancellationToken = default);
}
