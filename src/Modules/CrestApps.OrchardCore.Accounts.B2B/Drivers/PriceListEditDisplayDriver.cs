using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// Builds the editor of the price list page: the name, and the prices, each in its card.
/// </summary>
internal sealed class PriceListEditDisplayDriver : DisplayDriver<PriceListEditViewModel>
{
    internal readonly IStringLocalizer S;

    public PriceListEditDisplayDriver(IStringLocalizer<PriceListEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(PriceListEditViewModel model, BuildEditorContext context)
        => Combine(
            Initialize<PriceListEditViewModel>("PriceListGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<PriceListEditViewModel>("PriceListPrices_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Prices"], "20")));

    public override async Task<IDisplayResult> UpdateAsync(PriceListEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.PercentOff, m => m.Prices);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(PriceListEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(PriceListEditViewModel source, PriceListEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Name = source.Name;
        model.PercentOff = source.PercentOff;
        model.Prices = source.Prices;
        model.StoreCurrency = source.StoreCurrency;
    }
}
