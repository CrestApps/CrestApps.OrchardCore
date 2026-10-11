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
            model.Price = context.IsNew ? null : part.Price;
            model.Currency = string.IsNullOrEmpty(part.Currency)
                ? context.TypePartDefinition.GetSettings<ProductPartSettings>().DefaultCurrency
                : part.Currency;
            model.Sku = part.Sku;
            model.Currencies = await BuildCurrencyOptionsAsync(model.Currency);
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.Price.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price is required"]);
        }
        else if (model.Price < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price cannot be negative number."]);
        }

        if (model.Price.HasValue)
        {
            part.Price = model.Price.Value;
        }

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
