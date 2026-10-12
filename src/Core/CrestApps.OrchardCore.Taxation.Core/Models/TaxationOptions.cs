using System;
using System.Collections.Generic;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Core.Models;

/// <summary>
/// Configures the default behavior of the taxation engine.
/// </summary>
public sealed class TaxationOptions
{
    /// <summary>
    /// Gets or sets the number of decimal places tax amounts are rounded to. Defaults to <c>2</c>.
    /// </summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Gets or sets the midpoint rounding mode. Defaults to <see cref="MidpointRounding.AwayFromZero"/>.
    /// </summary>
    public MidpointRounding MidpointRounding { get; set; } = MidpointRounding.AwayFromZero;

    /// <summary>
    /// Gets or sets the level at which tax amounts are rounded. Defaults to <see cref="TaxRoundingLevel.Line"/>.
    /// </summary>
    public TaxRoundingLevel RoundingLevel { get; set; } = TaxRoundingLevel.Line;

    /// <summary>
    /// Gets or sets a per-currency override of the number of decimal places, keyed by currency code.
    /// </summary>
    public IDictionary<string, int> CurrencyDecimalPlaces { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
