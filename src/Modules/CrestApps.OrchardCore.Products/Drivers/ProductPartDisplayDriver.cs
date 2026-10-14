using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly IProductCurrencyProvider _currencyProvider;

    internal readonly IStringLocalizer S;

    public ProductPartDisplayDriver(
        IProductCurrencyProvider currencyProvider,
        IStringLocalizer<ProductPartDisplayDriver> stringLocalizer)
    {
        _currencyProvider = currencyProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
    {
        return Initialize<ProductPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<ProductPartSettings>();

            model.Price = context.IsNew ? null : part.Price;
            model.CompareAtPrice = part.CompareAtPrice;
            model.Currency = string.IsNullOrEmpty(part.Currency)
                ? settings.DefaultCurrency
                : part.Currency;
            model.Sku = part.Sku;
            model.IsPurchasable = part.IsPurchasable;
            model.PriceDisplay = part.PriceDisplay;
            model.ShowPurchasable = settings.SalesMode == ProductSalesMode.PerItem;
            model.ShowPriceDisplay = settings.AllowPriceDisplayOverride;
            model.PriceRequired = part.RequiresPrice(settings);
            model.Currencies = await BuildCurrencyOptionsAsync(model.Currency);
            model.PriceDisplays =
            [
                new SelectListItem(S["Use the content type's setting"], string.Empty),
                new SelectListItem(S["Show the price"], nameof(ProductPriceDisplay.Show)),
                new SelectListItem(S["Show on request"], nameof(ProductPriceDisplay.OnRequest)),
                new SelectListItem(S["Hide the price"], nameof(ProductPriceDisplay.Hidden)),
            ];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<ProductPartSettings>();

        if (settings.SalesMode == ProductSalesMode.PerItem)
        {
            part.IsPurchasable = model.IsPurchasable;
        }

        if (settings.AllowPriceDisplayOverride)
        {
            part.PriceDisplay = model.PriceDisplay;
        }

        // A price is needed only when the product can be bought or its price is shown. A house listed by
        // inquiry has none, and inventing a zero would show "$0" the moment the type's display changes.
        if (!model.Price.HasValue && part.RequiresPrice(settings))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price is required"]);
        }
        else if (model.Price < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price cannot be negative number."]);
        }

        if (model.CompareAtPrice < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CompareAtPrice), S["The compare-at price cannot be a negative number."]);
        }

        part.Price = model.Price;
        part.CompareAtPrice = model.CompareAtPrice;

        var normalizedCurrency = NormalizeCurrencyCode(model.Currency);

        if (!string.IsNullOrEmpty(normalizedCurrency) &&
            await _currencyProvider.FindByCodeAsync(normalizedCurrency) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currency), S["Select a valid currency."]);
        }

        part.Currency = normalizedCurrency;
        part.Sku = model.Sku?.Trim();

        return Edit(part, context);
    }

    private async Task<IEnumerable<SelectListItem>> BuildCurrencyOptionsAsync(string selectedCurrency)
    {
        var normalizedCurrency = NormalizeCurrencyCode(selectedCurrency);
        var currencies = await _currencyProvider.GetCurrenciesAsync();
        var options = currencies
            .Select(currency => new SelectListItem($"{currency.DisplayName} ({currency.CurrencyCode})", currency.CurrencyCode)
            {
                Selected = string.Equals(currency.CurrencyCode, normalizedCurrency, StringComparison.OrdinalIgnoreCase),
            })
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrEmpty(normalizedCurrency) &&
            options.All(item => !string.Equals(item.Value, normalizedCurrency, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new SelectListItem(normalizedCurrency, normalizedCurrency) { Selected = true });
        }

        return options;
    }

    private static string NormalizeCurrencyCode(string currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : currencyCode.Trim().ToUpperInvariant();
}
