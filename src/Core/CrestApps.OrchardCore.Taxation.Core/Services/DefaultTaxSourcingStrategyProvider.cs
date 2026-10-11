using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxSourcingStrategyProvider"/> that resolves sourcing strategies from the
/// registered <see cref="ITaxSourcingStrategy"/> instances by name.
/// </summary>
public sealed class DefaultTaxSourcingStrategyProvider : ITaxSourcingStrategyProvider
{
    private readonly Dictionary<string, ITaxSourcingStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxSourcingStrategyProvider"/> class.
    /// </summary>
    /// <param name="strategies">The registered sourcing strategies.</param>
    public DefaultTaxSourcingStrategyProvider(IEnumerable<ITaxSourcingStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(strategy => strategy.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ITaxSourcingStrategy GetStrategy(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _strategies.TryGetValue(name, out var strategy) ? strategy : null;
    }
}
