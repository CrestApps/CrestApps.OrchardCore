using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxCalculationMethodProvider"/> that resolves calculation methods from the
/// registered <see cref="ITaxCalculationMethod"/> instances by name.
/// </summary>
public sealed class DefaultTaxCalculationMethodProvider : ITaxCalculationMethodProvider
{
    private readonly Dictionary<string, ITaxCalculationMethod> _methods;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxCalculationMethodProvider"/> class.
    /// </summary>
    /// <param name="methods">The registered calculation methods.</param>
    public DefaultTaxCalculationMethodProvider(IEnumerable<ITaxCalculationMethod> methods)
    {
        _methods = methods.ToDictionary(method => method.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ITaxCalculationMethod GetMethod(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _methods.TryGetValue(name, out var method) ? method : null;
    }
}
