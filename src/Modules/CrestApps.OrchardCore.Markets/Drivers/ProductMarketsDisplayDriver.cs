using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Markets.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Markets.Drivers;

/// <summary>
/// "Not sold in" on a product's editor: the markets it's left out of. Its shoppers see that it isn't sold there, and
/// can't add it to their cart.
/// </summary>
internal sealed class ProductMarketsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly IMarketStore _markets;

    public ProductMarketsDisplayDriver(IMarketStore markets)
    {
        _markets = markets;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
        => Initialize<ProductMarketsEditViewModel>("ProductMarkets_Edit", async model =>
        {
            model.Markets = [.. (await _markets.GetAllAsync()).OrderBy(market => market.Name, StringComparer.OrdinalIgnoreCase)];
            model.NotSoldIn = part.ContentItem.TryGet<ProductMarketsPart>(out var markets) ? [.. markets.NotSoldIn ?? []] : [];
        }).Location("Content:22");

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductMarketsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.NotSoldIn);

        var known = (await _markets.GetAllAsync()).Select(market => market.ItemId).ToHashSet(StringComparer.Ordinal);

        part.ContentItem.Alter<ProductMarketsPart>(markets => markets.NotSoldIn = [.. (model.NotSoldIn ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal)]);

        return Edit(part, context);
    }
}
