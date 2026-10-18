using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly IProductCurrencyProvider _currencyProvider;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public ProductPartDisplayDriver(
        IProductCurrencyProvider currencyProvider,
        ILocalClock localClock,
        IStringLocalizer<ProductPartDisplayDriver> stringLocalizer)
    {
        _currencyProvider = currencyProvider;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
    {
        return Initialize<ProductPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<ProductPartSettings>();

            model.Price = context.IsNew ? null : part.Price;
            model.CompareAtPrice = part.CompareAtPrice;
            model.SalePrice = part.SalePrice;
            model.SaleStarts = await ToLocalAsync(part.SaleStartsUtc);
            model.SaleEnds = await ToLocalAsync(part.SaleEndsUtc);
            model.Currency = string.IsNullOrEmpty(part.Currency)
                ? settings.DefaultCurrency
                : part.Currency;
            model.Sku = part.Sku;
            model.IsPurchasable = part.IsPurchasable;
            model.PriceDisplay = part.PriceDisplay;
            model.Featured = part.Featured;
            model.Position = part.Position;
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

    // The editor takes the sale's dates in the site's time zone; the product keeps them in UTC.
    private async Task<DateTime?> ToLocalAsync(DateTime? utc)
        => utc is { } value ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(value, DateTimeKind.Utc))).DateTime : null;

    private async Task<DateTime> ToUtcAsync(DateTime local)
        => DateTime.SpecifyKind(await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)), DateTimeKind.Utc);

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

        if (model.SalePrice < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SalePrice), S["The sale price cannot be a negative number."]);
        }
        else if (model.SalePrice.HasValue && model.Price.HasValue && model.SalePrice >= model.Price)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SalePrice), S["The sale price has to be lower than the price."]);
        }

        if (model.SaleStarts.HasValue && model.SaleEnds.HasValue && model.SaleEnds <= model.SaleStarts)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SaleEnds), S["The sale has to end after it starts."]);
        }

        part.Price = model.Price;
        part.CompareAtPrice = model.CompareAtPrice;
        part.SalePrice = model.SalePrice;
        part.SaleStartsUtc = model.SaleStarts is { } starts ? await ToUtcAsync(starts) : null;
        part.SaleEndsUtc = model.SaleEnds is { } ends ? await ToUtcAsync(ends) : null;

        var normalizedCurrency = NormalizeCurrencyCode(model.Currency);

        if (!string.IsNullOrEmpty(normalizedCurrency) &&
            await _currencyProvider.FindByCodeAsync(normalizedCurrency) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currency), S["Select a valid currency."]);
        }

        part.Currency = normalizedCurrency;
        part.Sku = model.Sku?.Trim();
        part.Featured = model.Featured;
        part.Position = model.Position;

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
