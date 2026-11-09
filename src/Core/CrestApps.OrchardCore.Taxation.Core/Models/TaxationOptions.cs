using System;
using System.Collections.Generic;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Core.Models;

/// <summary>
/// Configures the default behavior of the taxation engine.
/// </summary>
public sealed class TaxationOptions
{
    /// <summary>
    /// Gets or sets the number of decimal places every tax amount is rounded to, whatever its currency. Defaults to
    /// <see langword="null"/>: each amount is rounded to its currency's precision from <c>CurrencyScale</c>, so yen
    /// tax has no decimals and Kuwaiti dinar tax three, the same as the payment.
    /// </summary>
    public int? DecimalPlaces { get; set; }

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
