using System;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxRoundingStrategy"/> that rounds values using the configured number of decimal
/// places and midpoint mode.
/// </summary>
public sealed class DefaultTaxRoundingStrategy : ITaxRoundingStrategy
{
    private readonly TaxationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxRoundingStrategy"/> class.
    /// </summary>
    /// <param name="options">The taxation options.</param>
    public DefaultTaxRoundingStrategy(IOptions<TaxationOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public decimal Round(decimal value, string currency)
    {
        var decimals = _options.DecimalPlaces;

        if (!string.IsNullOrEmpty(currency) && _options.CurrencyDecimalPlaces.TryGetValue(currency, out var currencyDecimals))
        {
            decimals = currencyDecimals;
        }

        return Math.Round(value, decimals, _options.MidpointRounding);
    }
}
