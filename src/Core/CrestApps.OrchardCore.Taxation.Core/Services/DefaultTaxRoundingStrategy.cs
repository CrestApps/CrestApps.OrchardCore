using System;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxRoundingStrategy"/> that rounds values to the currency's precision from
/// <see cref="CurrencyScale"/>, unless <see cref="TaxationOptions"/> sets a number of decimal places, and with the
/// configured midpoint mode.
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
        var decimals = _options.DecimalPlaces ?? CurrencyScale.GetDecimalPlaces(currency);

        if (!string.IsNullOrEmpty(currency) && _options.CurrencyDecimalPlaces?.TryGetValue(currency, out var currencyDecimals) == true)
        {
            decimals = currencyDecimals;
        }

        return Math.Round(value, decimals, _options.MidpointRounding);
    }
}
