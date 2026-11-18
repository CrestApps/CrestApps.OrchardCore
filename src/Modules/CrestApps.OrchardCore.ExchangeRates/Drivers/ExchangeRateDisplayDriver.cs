using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.ExchangeRates.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ExchangeRates.Drivers;

/// <summary>
/// The row of an exchange rate in its list, in both layouts, and its editor.
/// </summary>
public sealed class ExchangeRateDisplayDriver : DisplayDriver<ExchangeRate>
{
    private readonly IExchangeRateService _rates;
    private readonly ILocalClock _localClock;

    public ExchangeRateDisplayDriver(IExchangeRateService rates, ILocalClock localClock)
    {
        _rates = rates;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ExchangeRate rate, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{rate.Currency} {rate.BaseCurrency} {rate.Date:yyyy-MM-dd}".ToLowerInvariant();
        }

        return CombineAsync(
            View("ExchangeRate_Fields_SummaryAdmin", rate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ExchangeRate_Tags_SummaryAdmin", rate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Rate:5"),
            View("ExchangeRate_Buttons_SummaryAdmin", rate).Location("Actions:5"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ExchangeRate rate, BuildEditorContext context)
        => Initialize<ExchangeRateViewModel>("ExchangeRateFields_Edit", async model =>
        {
            model.Currency = rate.Currency;
            model.BaseCurrency = rate.BaseCurrency ?? await _rates.GetBaseCurrencyAsync();
            model.Rate = rate.Rate;
            model.Date = rate.Date == default ? DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime) : rate.Date;
            model.Source = rate.Source;
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ExchangeRate rate, UpdateEditorContext context)
    {
        var model = new ExchangeRateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        rate.Currency = model.Currency?.Trim().ToUpperInvariant();
        rate.BaseCurrency = model.BaseCurrency?.Trim().ToUpperInvariant();
        rate.Rate = model.Rate;
        rate.Date = model.Date;

        // A rate staff change by hand is theirs, wherever it came from.
        rate.Source = ExchangeRatesConstants.ManualSource;

        return Edit(rate, context);
    }
}
