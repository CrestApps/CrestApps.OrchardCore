using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Quotes and saves the Shipping step from code with the rules of its page, through
/// <see cref="ShippingCheckoutStepService"/>.
/// </summary>
public sealed class ShippingCheckoutFulfillmentStep : ICheckoutFulfillmentStep
{
    private readonly ShippingCheckoutStepService _shippingStep;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippingCheckoutFulfillmentStep"/> class.
    /// </summary>
    /// <param name="shippingStep">The Shipping step's service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShippingCheckoutFulfillmentStep(
        ShippingCheckoutStepService shippingStep,
        IStringLocalizer<ShippingCheckoutFulfillmentStep> stringLocalizer)
    {
        _shippingStep = shippingStep;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string StepKey
        => ShippingConstants.CheckoutStepKey;

    /// <inheritdoc />
    public async Task<CheckoutFulfillmentQuote> QuoteAsync(CheckoutFlow flow, Address address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!HasStep(flow))
        {
            return new CheckoutFulfillmentQuote { RequiresShipping = false };
        }

        if (string.IsNullOrEmpty(address?.Country))
        {
            return new CheckoutFulfillmentQuote { Problem = S["Give the address's country, at least."] };
        }

        if (await _shippingStep.QuoteAsync(flow, address) is not { } quote)
        {
            return new CheckoutFulfillmentQuote { Problem = S["The order this checkout pays for is gone."] };
        }

        var currency = GetCurrency(flow);
        var options = (quote.Rates ?? [])
            .Select(rate => new CheckoutFulfillmentOption
            {
                Id = rate.RateId,
                Title = rate.Name,
                Description = rate.Description,
                Carrier = rate.ProviderName,
                Amount = rate.Amount,
                Currency = rate.Currency ?? currency,
            })
            .ToList();

        return new CheckoutFulfillmentQuote
        {
            Options = options,
            Problem = options.Count == 0 ? S["The store doesn't ship to this address."] : null,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckoutPickupOption>> GetPickupOptionsAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!HasStep(flow))
        {
            return [];
        }

        var currency = GetCurrency(flow);

        return (await _shippingStep.GetPickupLocationsAsync())
            .Select(location => new CheckoutPickupOption
            {
                Id = location.LocationId,
                Name = location.Name,
                Address = location.ToAddress(),
                Instructions = location.Instructions,
                Fee = location.Fee,
                Currency = currency,
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CheckoutStepSaveResult> SaveAsync(CheckoutFlow flow, CheckoutFulfillmentChoice choice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(choice);

        var modelState = new ModelStateDictionary();
        var result = await _shippingStep.SaveAsync(flow, new ShippingStepInput
        {
            PickupLocationId = choice.PickupLocationId,
            Address = AddressEditorViewModel.From(choice.Address),
            RateId = choice.OptionId,
        }, modelState, prefix: null);

        return result.Saved
            ? CheckoutStepSaveResult.Success()
            : CheckoutStepSaveResults.NotSaved(modelState, result.IsChoosing);
    }

    private static bool HasStep(CheckoutFlow flow)
        => flow.Session.Steps?.Any(step => string.Equals(step.Key, ShippingConstants.CheckoutStepKey, StringComparison.Ordinal)) == true;

    private static string GetCurrency(CheckoutFlow flow)
        => flow.Session is IEntity entity && entity.TryGet<CheckoutInvoice>(out var invoice) && !string.IsNullOrEmpty(invoice.Currency)
            ? invoice.Currency
            : flow.Session.Currency;
}
