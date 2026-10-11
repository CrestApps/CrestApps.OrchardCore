using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxableItemResolver"/> that delegates to the registered
/// <see cref="ITaxableItemProvider"/> instances in priority order.
/// </summary>
public sealed class DefaultTaxableItemResolver : ITaxableItemResolver
{
    private readonly IReadOnlyList<ITaxableItemProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxableItemResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered taxable item providers.</param>
    public DefaultTaxableItemResolver(IEnumerable<ITaxableItemProvider> providers)
    {
        _providers = providers.OrderBy(provider => provider.Order).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<ITaxableItem> ResolveAsync(object source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var provider in _providers)
        {
            if (provider.CanCreate(source))
            {
                var item = await provider.CreateAsync(source, cancellationToken);

                if (item is not null)
                {
                    return item;
                }
            }
        }

        return null;
    }
}
