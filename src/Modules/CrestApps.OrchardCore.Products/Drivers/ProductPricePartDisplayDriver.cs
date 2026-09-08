using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Edits the set of prices a product is offered at.
/// </summary>
/// <remarks>
/// A price keeps its identifier for its whole life, because an agreement created from it names it and a
/// gateway price is reused by it. Editing a price therefore changes what new buyers pay, never what an
/// existing agreement bills — and withdrawing one deactivates it rather than deleting it.
/// </remarks>
public sealed class ProductPricePartDisplayDriver : ContentPartDisplayDriver<ProductPricePart>
{
    private readonly IProductCurrencyProvider _currencyProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPricePartDisplayDriver"/> class.
    /// </summary>
    /// <param name="currencyProvider">The currency catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductPricePartDisplayDriver(
        IProductCurrencyProvider currencyProvider,
        IStringLocalizer<ProductPricePartDisplayDriver> stringLocalizer)
    {
        _currencyProvider = currencyProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ProductPricePart part, BuildPartEditorContext context)
    {
        return Initialize<ProductPricePartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Prices = [.. (part.Prices ?? []).Select(ToViewModel)];

            // A product with no prices still needs one row to fill in, or there is nothing on the page to
            // type into and nothing for the browser to copy when adding a second.
            if (model.Prices.Count == 0)
            {
                model.Prices.Add(new ProductPriceViewModel { IsDefault = true });
            }
            model.Currencies = await BuildCurrencyOptionsAsync();
            model.Kinds =
            [
                new SelectListItem(S["Charged once"], nameof(PriceKind.OneTime)),
                new SelectListItem(S["Recurring"], nameof(PriceKind.Recurring)),
            ];
            model.Intervals = Enum.GetValues<BillingInterval>()
                .Select(value => new SelectListItem(value.ToString(), value.ToString()))
                .ToArray();
        });
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ProductPricePart part, UpdatePartEditorContext context)
    {
        var model = new ProductPricePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var prices = new List<ProductPrice>();
        var index = -1;

        foreach (var entry in model.Prices ?? [])
        {
            index++;

            if (entry is null || entry.Remove)
            {
                continue;
            }

            // The blank row the editor always offers is not an attempt to add a price, so it is dropped
            // rather than reported as invalid.
            if (ProductPriceEditor.IsBlank(entry.Amount, entry.Name))
            {
                continue;
            }

            var price = ToPrice(entry);
            var errors = ProductPriceEditor.Validate(price);

            foreach (var error in errors)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"Prices[{index}].{error.Field}", Describe(error.Kind));
            }

            if (errors.Count == 0)
            {
                prices.Add(price);
            }
        }

        var defaultError = ProductPriceEditor.SettleDefault(prices);

        if (defaultError is not null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Prices), Describe(defaultError.Kind));
        }

        part.Prices = prices;

        return Edit(part, context);
    }

    private string Describe(ProductPriceErrorKind kind)
        => kind switch
        {
            ProductPriceErrorKind.NegativeAmount => S["Enter an amount of zero or more."],
            ProductPriceErrorKind.MissingBillingDuration => S["A recurring price needs a billing duration of at least one."],
            ProductPriceErrorKind.MissingInterval => S["A recurring price needs a billing interval."],
            ProductPriceErrorKind.MinimumAboveMaximum => S["The minimum cannot be more than the maximum."],
            ProductPriceErrorKind.NegativeTrial => S["A trial cannot be a negative number of days."],
            ProductPriceErrorKind.EndsBeforeItStarts => S["The end of the offer must come after its start."],
            ProductPriceErrorKind.SeveralDefaults => S["Only one price can be the default."],
            _ => S["That price cannot be saved."],
        };

    private static ProductPrice ToPrice(ProductPriceViewModel entry)
        => new()
        {
            // A blank id means the editor added the row; anything else keeps the id it already had, because
            // agreements and gateway prices name it.
            PriceId = string.IsNullOrEmpty(entry.PriceId) ? IdGenerator.GenerateId() : entry.PriceId,
            Name = entry.Name?.Trim(),
            Currency = string.IsNullOrWhiteSpace(entry.Currency) ? null : entry.Currency.Trim().ToUpperInvariant(),
            Amount = entry.Amount ?? 0m,
            Kind = entry.Kind,
            BillingDuration = entry.Kind == PriceKind.Recurring ? entry.BillingDuration : null,
            Interval = entry.Kind == PriceKind.Recurring ? entry.Interval : null,
            BillingCycleLimit = entry.Kind == PriceKind.Recurring ? entry.BillingCycleLimit : null,
            StartDayDelay = entry.Kind == PriceKind.Recurring ? entry.StartDayDelay : null,
            TrialDays = entry.Kind == PriceKind.Recurring ? entry.TrialDays : null,
            SetupFee = entry.SetupFee,
            SetupFeeDescription = entry.SetupFeeDescription?.Trim(),
            AllowCustomAmount = entry.AllowCustomAmount,
            MinimumAmount = entry.AllowCustomAmount ? entry.MinimumAmount : null,
            MaximumAmount = entry.AllowCustomAmount ? entry.MaximumAmount : null,
            AllowQuantity = entry.AllowQuantity,
            MaximumQuantity = entry.AllowQuantity ? entry.MaximumQuantity : null,
            IsDefault = entry.IsDefault,
            IsActive = entry.IsActive,
            EffectiveFromUtc = entry.EffectiveFromUtc,
            EffectiveToUtc = entry.EffectiveToUtc,
        };

    private static ProductPriceViewModel ToViewModel(ProductPrice price)
        => new()
        {
            PriceId = price.PriceId,
            Name = price.Name,
            Currency = price.Currency,
            Amount = price.Amount,
            Kind = price.Kind,
            BillingDuration = price.BillingDuration,
            Interval = price.Interval,
            BillingCycleLimit = price.BillingCycleLimit,
            StartDayDelay = price.StartDayDelay,
            TrialDays = price.TrialDays,
            SetupFee = price.SetupFee,
            SetupFeeDescription = price.SetupFeeDescription,
            AllowCustomAmount = price.AllowCustomAmount,
            MinimumAmount = price.MinimumAmount,
            MaximumAmount = price.MaximumAmount,
            AllowQuantity = price.AllowQuantity,
            MaximumQuantity = price.MaximumQuantity,
            IsDefault = price.IsDefault,
            IsActive = price.IsActive,
            EffectiveFromUtc = price.EffectiveFromUtc,
            EffectiveToUtc = price.EffectiveToUtc,
        };

    private async Task<IEnumerable<SelectListItem>> BuildCurrencyOptionsAsync()
    {
        var currencies = await _currencyProvider.GetCurrenciesAsync();

        return currencies
            .Select(currency => new SelectListItem($"{currency.DisplayName} ({currency.CurrencyCode})", currency.CurrencyCode))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
