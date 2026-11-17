using System;
using System.Collections.Generic;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Configures the tax calculation methods that operators can choose from when they add a tax rule. Each
/// registered method becomes a rule source, so the "Add tax rule" dialog and the per-source editors are
/// driven by this list rather than by a hard-coded dropdown.
/// </summary>
public sealed class TaxCalculationMethodOptions
{
    private readonly Dictionary<string, TaxCalculationMethodEntry> _methods = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered calculation methods keyed by their unique name.
    /// </summary>
    public IReadOnlyDictionary<string, TaxCalculationMethodEntry> Methods
        => _methods;

    /// <summary>
    /// Adds or updates a calculation method entry.
    /// </summary>
    /// <param name="name">The unique calculation method name.</param>
    /// <param name="configure">An optional action that configures the entry.</param>
    public void AddMethod(string name, Action<TaxCalculationMethodEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!_methods.TryGetValue(name, out var entry))
        {
            entry = new TaxCalculationMethodEntry(name);
        }

        configure?.Invoke(entry);

        if (entry.DisplayName is null || string.IsNullOrEmpty(entry.DisplayName.Value))
        {
            entry.DisplayName = new LocalizedString(name, name);
        }

        _methods[name] = entry;
    }
}
