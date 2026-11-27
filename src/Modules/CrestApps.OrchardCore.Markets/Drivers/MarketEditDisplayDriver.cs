using CrestApps.OrchardCore.Markets.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Markets.Drivers;

/// <summary>
/// Builds the editor of a market, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class MarketEditDisplayDriver : DisplayDriver<MarketEditViewModel>
{
    internal readonly IStringLocalizer S;

    public MarketEditDisplayDriver(IStringLocalizer<MarketEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MarketEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<MarketEditViewModel>("MarketGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Market"], "10")),
            Initialize<MarketEditViewModel>("MarketPricing_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Pricing"], "20")),
            Initialize<MarketEditViewModel>("MarketLanguage_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Language"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(MarketEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Countries,
            m => m.Currency,
            m => m.ExchangeRate,
            m => m.Rounding,
            m => m.Culture,
            m => m.PricesIncludeTax,
            m => m.IsEnabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(MarketEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(MarketEditViewModel source, MarketEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Name = source.Name;
        model.Countries = source.Countries;
        model.Currency = source.Currency;
        model.ExchangeRate = source.ExchangeRate;
        model.Rounding = source.Rounding;
        model.Culture = source.Culture;
        model.PricesIncludeTax = source.PricesIncludeTax;
        model.IsEnabled = source.IsEnabled;
        model.StoreCurrency = source.StoreCurrency;
        model.Roundings = source.Roundings;
        model.Cultures = source.Cultures;
    }
}
