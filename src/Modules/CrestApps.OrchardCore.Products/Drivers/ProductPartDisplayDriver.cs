using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private const int TierRows = 4;
    private const int RoleRows = 3;
    private const int AddOnRows = 4;

    private readonly IProductCurrencyProvider _currencyProvider;
    private readonly ILocalClock _localClock;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public ProductPartDisplayDriver(
        IProductCurrencyProvider currencyProvider,
        ILocalClock localClock,
        IServiceProvider serviceProvider,
        IStringLocalizer<ProductPartDisplayDriver> stringLocalizer)
    {
        _currencyProvider = currencyProvider;
        _localClock = localClock;
        _serviceProvider = serviceProvider;
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
            model.ExternalUrl = part.ExternalUrl;
            model.ExternalButtonText = part.ExternalButtonText;
            model.MinQuantity = part.MinQuantity;
            model.MaxQuantity = part.MaxQuantity;
            model.QuantityStep = part.QuantityStep;
            model.PriceTiers = [.. (part.PriceTiers ?? []).OrderBy(tier => tier.MinQuantity).Select(tier => new ProductPriceTierViewModel { MinQuantity = tier.MinQuantity, PercentOff = tier.PercentOff })];
            model.RoleDiscounts = [.. (part.RoleDiscounts ?? []).Select(entry => new ProductRoleDiscountViewModel { Role = entry.Role, PercentOff = entry.PercentOff })];

            // Empty rows to add more, up to the limit.
            while (model.PriceTiers.Count < TierRows)
            {
                model.PriceTiers.Add(new ProductPriceTierViewModel());
            }

            while (model.RoleDiscounts.Count < RoleRows)
            {
                model.RoleDiscounts.Add(new ProductRoleDiscountViewModel());
            }

            model.Roles = await GetRolesAsync();
            model.AddOns = [.. (part.AddOns ?? []).Select(addOn => new ProductAddOnViewModel
            {
                AddOnId = addOn.AddOnId,
                Label = addOn.Label,
                Kind = addOn.Kind,
                Price = addOn.Price == 0 ? null : addOn.Price,
                PriceBasis = addOn.PriceBasis,
                Required = addOn.Required,
            })];

            while (model.AddOns.Count < AddOnRows)
            {
                model.AddOns.Add(new ProductAddOnViewModel());
            }
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

    // The site's roles, but Anonymous: a role discount is for signed-in buyers.
    private async Task<IList<SelectListItem>> GetRolesAsync()
    {
        if (_serviceProvider.GetService(typeof(global::OrchardCore.Security.Services.IRoleService)) is not global::OrchardCore.Security.Services.IRoleService roles)
        {
            return [];
        }

        return [.. (await roles.GetRolesAsync()).Select(role => role.RoleName)
            .Where(name => !string.Equals(name, "Anonymous", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new SelectListItem(name, name))];
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
        if (!model.Price.HasValue && part.RequiresPrice(settings) && model.ProductIds?.Exists(id => !string.IsNullOrWhiteSpace(id)) != true)
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

        if (model.MinQuantity < 1 || model.MaxQuantity < 1 || model.QuantityStep < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinQuantity), S["Quantities have to be 1 or more."]);
        }
        else if (model.MinQuantity.HasValue && model.MaxQuantity.HasValue && model.MaxQuantity < model.MinQuantity)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxQuantity), S["The most a buyer can buy can't be less than the least."]);
        }

        var externalUrl = model.ExternalUrl?.Trim();

        if (!string.IsNullOrEmpty(externalUrl) &&
            (!Uri.TryCreate(externalUrl, UriKind.Absolute, out var external) || (external.Scheme != Uri.UriSchemeHttps && external.Scheme != Uri.UriSchemeHttp)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ExternalUrl), S["Enter a full web address, starting with https://."]);
        }

        part.ExternalUrl = string.IsNullOrEmpty(externalUrl) ? null : externalUrl;
        part.ExternalButtonText = string.IsNullOrWhiteSpace(model.ExternalButtonText) ? null : model.ExternalButtonText.Trim();
        part.MinQuantity = model.MinQuantity is > 1 ? model.MinQuantity : null;
        part.MaxQuantity = model.MaxQuantity;
        part.QuantityStep = model.QuantityStep is > 1 ? model.QuantityStep : null;

        var tiers = (model.PriceTiers ?? []).Where(tier => tier.MinQuantity.HasValue || tier.PercentOff.HasValue).ToList();

        if (tiers.Any(tier => tier.MinQuantity is not > 1 || tier.PercentOff is not (> 0 and <= 100)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PriceTiers), S["Each volume discount needs a quantity of 2 or more and a percentage between 0 and 100."]);
        }

        part.PriceTiers = [.. tiers
            .Where(tier => tier.MinQuantity > 1 && tier.PercentOff is > 0 and <= 100)
            .GroupBy(tier => tier.MinQuantity.Value)
            .Select(group => new ProductPriceTier { MinQuantity = group.Key, PercentOff = group.Max(tier => tier.PercentOff.Value) })
            .OrderBy(tier => tier.MinQuantity)];

        var roleDiscounts = (model.RoleDiscounts ?? []).Where(entry => !string.IsNullOrEmpty(entry.Role) || entry.PercentOff.HasValue).ToList();

        if (roleDiscounts.Any(entry => string.IsNullOrEmpty(entry.Role) || entry.PercentOff is not (> 0 and <= 100)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RoleDiscounts), S["Each role discount needs a role and a percentage between 0 and 100."]);
        }

        part.RoleDiscounts = [.. roleDiscounts
            .Where(entry => !string.IsNullOrEmpty(entry.Role) && entry.PercentOff is > 0 and <= 100)
            .GroupBy(entry => entry.Role, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProductRoleDiscount { Role = group.Key, PercentOff = group.Max(entry => entry.PercentOff.Value) })];

        // A row without a label is an empty one. An add-on keeps its id, so carts holding it still match.
        var addOns = (model.AddOns ?? []).Where(addOn => !string.IsNullOrWhiteSpace(addOn.Label)).ToList();

        if (addOns.Any(addOn => addOn.Price < 0))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AddOns), S["An add-on's price can't be less than zero."]);
        }

        part.AddOns = [.. addOns.Select(addOn => new ProductAddOn
        {
            AddOnId = string.IsNullOrEmpty(addOn.AddOnId) ? IdGenerator.GenerateId() : addOn.AddOnId,
            Label = addOn.Label.Trim(),
            Kind = Enum.IsDefined(addOn.Kind) ? addOn.Kind : ProductAddOnKind.Text,
            Price = Math.Max(0m, addOn.PriceBasis == ProductAddOnPriceBasis.PercentOfPrice ? Math.Min(100m, addOn.Price ?? 0m) : addOn.Price ?? 0m),
            PriceBasis = Enum.IsDefined(addOn.PriceBasis) ? addOn.PriceBasis : ProductAddOnPriceBasis.PerUnit,
            Required = addOn.Required,
        })];

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
