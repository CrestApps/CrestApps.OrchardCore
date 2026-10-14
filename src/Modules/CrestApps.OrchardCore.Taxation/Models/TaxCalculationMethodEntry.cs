using System;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Describes a tax calculation method that operators can select as the source of a tax rule.
/// </summary>
public sealed class TaxCalculationMethodEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCalculationMethodEntry"/> class.
    /// </summary>
    /// <param name="name">The unique calculation method name.</param>
    public TaxCalculationMethodEntry(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }

    /// <summary>
    /// Gets the unique calculation method name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the display name shown in the method selection dialog.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown in the method selection dialog.
    /// </summary>
    public LocalizedString Description { get; set; }
}
