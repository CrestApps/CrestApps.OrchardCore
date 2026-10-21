using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The fields every shipping method has.
/// </summary>
internal sealed class ShippingMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    private const decimal MaxAmount = 1_000_000;

    private readonly IEnumerable<IShippingRateProvider> _providers;
    private readonly IProductCurrencyProvider _currencyProvider;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public ShippingMethodDisplayDriver(
        IEnumerable<IShippingRateProvider> providers,
        IProductCurrencyProvider currencyProvider,
        ISiteService siteService,
        IStringLocalizer<ShippingMethodDisplayDriver> stringLocalizer)
    {
        _providers = providers;
        _currencyProvider = currencyProvider;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
        => Initialize<ShippingMethodViewModel>("ShippingMethodFields_Edit", async model =>
        {
            var settings = await _siteService.GetSettingsAsync<ShippingSettings>();

            model.Name = method.Name;
            model.Description = method.Description;
            model.Enabled = method.Enabled;
            model.Position = method.Position;
            model.Currency = method.Currency;
            model.TaxCategoryCode = method.TaxCategoryCode;
            model.ProviderDisplayName = _providers.FirstOrDefault(provider => string.Equals(provider.Name, method.ProviderName, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? method.ProviderName;
            model.Currencies = (await _currencyProvider.GetCurrenciesAsync())
                .Select(currency => new SelectListItem($"{currency.CurrencyCode} ({currency.DisplayName})", currency.CurrencyCode, string.Equals(currency.CurrencyCode, method.Currency, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            model.Surcharges = (settings.ShippingClasses ?? [])
                .Union((IEnumerable<string>)method.ClassSurcharges?.Keys ?? [], StringComparer.OrdinalIgnoreCase)
                .Select(shippingClass => new ShippingClassSurchargeViewModel
                {
                    ShippingClass = shippingClass,
                    Amount = method.ClassSurcharges?.TryGetValue(shippingClass, out var amount) == true ? amount : null,
                })
                .ToList();
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        var model = new ShippingMethodViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Enter the name buyers see, such as Standard."]);
        }

        method.Name = model.Name?.Trim();
        method.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        method.Enabled = model.Enabled;
        method.Position = model.Position;
        method.Currency = string.IsNullOrWhiteSpace(model.Currency) ? null : model.Currency.Trim().ToUpperInvariant();
        method.TaxCategoryCode = string.IsNullOrWhiteSpace(model.TaxCategoryCode) ? null : model.TaxCategoryCode.Trim();
        method.ClassSurcharges = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var surcharge in model.Surcharges ?? [])
        {
            if (string.IsNullOrWhiteSpace(surcharge.ShippingClass) || surcharge.Amount is not { } amount || amount == 0)
            {
                continue;
            }

            if (amount is < 0 or > MaxAmount)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Surcharges), S["Enter surcharges from 0 to {0}.", MaxAmount]);

                continue;
            }

            method.ClassSurcharges[surcharge.ShippingClass.Trim()] = amount;
        }

        return Edit(method, context);
    }
}

/// <summary>
/// The settings of a flat rate method.
/// </summary>
internal sealed class FlatRateMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    internal readonly IStringLocalizer S;

    public FlatRateMethodDisplayDriver(IStringLocalizer<FlatRateMethodDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<FlatRateSettingsViewModel>("FlatRateSettings_Edit", model =>
        {
            var settings = method.GetOrCreate<FlatRateSettings>();

            model.Amount = settings.Amount;
            model.PerItemAmount = settings.PerItemAmount;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new FlatRateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Amount < 0 || model.PerItemAmount < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Amount), S["Amounts can't be negative."]);
        }

        method.Put(new FlatRateSettings { Amount = model.Amount, PerItemAmount = model.PerItemAmount });

        return Edit(method, context);
    }

    private static bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, ShippingConstants.Providers.Flat, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The settings of a table rate method.
/// </summary>
internal sealed class TableRateMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    private const int BlankRows = 3;

    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public TableRateMethodDisplayDriver(ISiteService siteService, IStringLocalizer<TableRateMethodDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<TableRateSettingsViewModel>("TableRateSettings_Edit", async model =>
        {
            var settings = method.GetOrCreate<TableRateSettings>();

            model.Basis = settings.Basis;
            model.Maximum = settings.Maximum;
            model.WeightUnit = (await _siteService.GetSettingsAsync<ShippingSettings>()).WeightUnit;

            // The rows there are, then a few blank ones to add more.
            model.Rows = settings.Rows
                .OrderBy(row => row.From)
                .Select(row => new TableRateRowViewModel { From = row.From, Amount = row.Amount })
                .Concat(Enumerable.Range(0, BlankRows).Select(_ => new TableRateRowViewModel()))
                .ToList();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new TableRateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rows = (model.Rows ?? [])
            .Where(row => row.From.HasValue && row.Amount.HasValue)
            .Select(row => new TableRateRow { From = row.From.Value, Amount = row.Amount.Value })
            .ToList();

        if (rows.Any(row => row.From < 0 || row.Amount < 0))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Rows), S["Values and amounts can't be negative."]);
        }
        else if (rows.GroupBy(row => row.From).Any(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Rows), S["Each row needs its own starting value."]);
        }
        else if (rows.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Rows), S["Add at least one row, such as from 0."]);
        }

        if (model.Maximum is < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Maximum), S["The limit can't be negative."]);
        }

        method.Put(new TableRateSettings
        {
            Basis = Enum.IsDefined(model.Basis) ? model.Basis : TableRateBasis.Weight,
            Maximum = model.Maximum,
            Rows = [.. rows.OrderBy(row => row.From)],
        });

        return Edit(method, context);
    }

    private static bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, ShippingConstants.Providers.Table, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The settings of a free shipping method.
/// </summary>
internal sealed class FreeShippingMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    internal readonly IStringLocalizer S;

    public FreeShippingMethodDisplayDriver(IStringLocalizer<FreeShippingMethodDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<FreeShippingSettingsViewModel>("FreeShippingSettings_Edit", model =>
        {
            model.MinimumSubtotal = method.GetOrCreate<FreeShippingSettings>().MinimumSubtotal;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new FreeShippingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MinimumSubtotal < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumSubtotal), S["The subtotal can't be negative."]);
        }

        method.Put(new FreeShippingSettings { MinimumSubtotal = model.MinimumSubtotal });

        return Edit(method, context);
    }

    private static bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, ShippingConstants.Providers.Free, StringComparison.OrdinalIgnoreCase);
}

internal sealed class LocalDeliveryMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    private const int MaxPostalCodes = 1000;

    internal readonly IStringLocalizer S;

    public LocalDeliveryMethodDisplayDriver(IStringLocalizer<LocalDeliveryMethodDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<LocalDeliverySettingsViewModel>("LocalDeliverySettings_Edit", model =>
        {
            var settings = method.GetOrCreate<LocalDeliverySettings>();

            model.PostalCodes = string.Join(Environment.NewLine, settings.PostalCodes ?? []);
            model.Amount = settings.Amount;
            model.FreeFromSubtotal = settings.FreeFromSubtotal;
            model.MinimumSubtotal = settings.MinimumSubtotal;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new LocalDeliverySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var postalCodes = ParsePostalCodes(model.PostalCodes);

        if (postalCodes.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PostalCodes), S["Enter the postal codes you deliver to."]);
        }

        if (model.Amount < 0 || model.FreeFromSubtotal < 0 || model.MinimumSubtotal < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Amount), S["Amounts can't be negative."]);
        }

        method.Put(new LocalDeliverySettings
        {
            PostalCodes = postalCodes,
            Amount = model.Amount,
            FreeFromSubtotal = model.FreeFromSubtotal,
            MinimumSubtotal = model.MinimumSubtotal,
        });

        return Edit(method, context);
    }

    // One or more per line, separated by commas; a trailing * covers every code starting with what's before it.
    internal static List<string> ParsePostalCodes(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => code != "*")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxPostalCodes)
            .ToList();

    private static bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, ShippingConstants.Providers.LocalDelivery, StringComparison.OrdinalIgnoreCase);
}

internal sealed class CarrierRateMethodDisplayDriver : DisplayDriver<ShippingMethod>
{
    private const int MaxCarriers = 20;

    private readonly ICarrierProviderResolver _carriers;

    internal readonly IStringLocalizer S;

    public CarrierRateMethodDisplayDriver(ICarrierProviderResolver carriers, IStringLocalizer<CarrierRateMethodDisplayDriver> stringLocalizer)
    {
        _carriers = carriers;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<CarrierRateSettingsViewModel>("CarrierRateSettings_Edit", async model =>
        {
            var settings = method.GetOrCreate<CarrierRateSettings>();

            model.Carriers = string.Join(", ", settings.Carriers ?? []);
            model.MaxRates = settings.MaxRates;
            model.MarkupPercent = settings.MarkupPercent;
            model.MarkupAmount = settings.MarkupAmount;
            model.FallbackAmount = settings.FallbackAmount;

            model.HasCarrierAccount = await _carriers.GetDefaultAsync() is not null;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new CarrierRateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MarkupAmount < 0 || model.FallbackAmount < 0 || model.MaxRates < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MarkupAmount), S["Amounts can't be negative."]);
        }

        method.Put(new CarrierRateSettings
        {
            Carriers = (model.Carriers ?? string.Empty)
                .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxCarriers)
                .ToList(),
            MaxRates = Math.Max(0, model.MaxRates),
            MarkupPercent = model.MarkupPercent,
            MarkupAmount = Math.Max(0, model.MarkupAmount),
            FallbackAmount = model.FallbackAmount is >= 0 ? model.FallbackAmount : null,
        });

        return Edit(method, context);
    }

    private static bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, ShippingConstants.Providers.Carrier, StringComparison.OrdinalIgnoreCase);
}
