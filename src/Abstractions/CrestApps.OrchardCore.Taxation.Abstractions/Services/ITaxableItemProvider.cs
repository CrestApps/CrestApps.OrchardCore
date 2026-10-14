using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Converts an arbitrary object into an <see cref="ITaxableItem"/>. Third-party modules register their
/// own providers so that products, subscriptions, bookings, content items, and custom objects can all
/// participate in taxation without modifying the framework.
/// </summary>
public interface ITaxableItemProvider
{
    /// <summary>
    /// Gets the priority of the provider. Lower values are evaluated first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Determines whether the provider can convert the supplied source object.
    /// </summary>
    /// <param name="source">The source object to convert.</param>
    /// <returns><see langword="true"/> when the provider can convert the source.</returns>
    bool CanCreate(object source);

    /// <summary>
    /// Creates a taxable item from the supplied source object.
    /// </summary>
    /// <param name="source">The source object to convert.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created taxable item, or <see langword="null"/> when the source cannot be converted.</returns>
    ValueTask<ITaxableItem> CreateAsync(object source, CancellationToken cancellationToken = default);
}
