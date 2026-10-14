using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ProductPart>
{
    private readonly IProductCurrencyProvider _currencyProvider;

    internal readonly IStringLocalizer S;

    public ProductPartSettingsDisplayDriver(
        IProductCurrencyProvider currencyProvider,
        IStringLocalizer<ProductPartSettingsDisplayDriver> stringLocalizer)
    {
        _currencyProvider = currencyProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ProductPartSettingsViewModel>("ProductPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ProductPartSettings>();

            model.Type = settings.Type;
            model.DefaultCurrency = settings.DefaultCurrency;
            model.SalesMode = settings.SalesMode;
            model.PriceDisplay = settings.PriceDisplay;
            model.OnRequestLabel = settings.OnRequestLabel;
            model.AllowPriceDisplayOverride = settings.AllowPriceDisplayOverride;
            model.SalesModes =
            [
                new SelectListItem(S["Purchasable: every product can be bought online"], nameof(ProductSalesMode.Purchasable)),
                new SelectListItem(S["Listing only: products are listed but never sold online"], nameof(ProductSalesMode.ListingOnly)),
                new SelectListItem(S["Per item: each product decides"], nameof(ProductSalesMode.PerItem)),
            ];
            model.PriceDisplays =
            [
                new SelectListItem(S["Show the price"], nameof(ProductPriceDisplay.Show)),
                new SelectListItem(S["Show on request (a label instead of the price)"], nameof(ProductPriceDisplay.OnRequest)),
                new SelectListItem(S["Hide the price"], nameof(ProductPriceDisplay.Hidden)),
            ];
            model.Types =
            [
                new SelectListItem(S["Undefined"], nameof(ProductType.Undefined)),
                new SelectListItem(S["Good"], nameof(ProductType.Good)),
                new SelectListItem(S["Service"], nameof(ProductType.Service)),
                new SelectListItem(S["Digital"], nameof(ProductType.Digital)),
            ];
            model.Currencies = await BuildCurrencyOptionsAsync(model.DefaultCurrency);
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ProductPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var normalizedCurrency = NormalizeCurrencyCode(model.DefaultCurrency);

        if (!string.IsNullOrEmpty(normalizedCurrency) &&
            await _currencyProvider.FindByCodeAsync(normalizedCurrency) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultCurrency), S["Select a valid default currency."]);
        }

        context.Builder.WithSettings(new ProductPartSettings()
        {
            Type = model.Type,
            DefaultCurrency = normalizedCurrency,
            SalesMode = model.SalesMode,
            PriceDisplay = model.PriceDisplay,
            OnRequestLabel = model.OnRequestLabel?.Trim(),
            AllowPriceDisplayOverride = model.AllowPriceDisplayOverride,
        });

        return Edit(contentTypePartDefinition, context);
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
