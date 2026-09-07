using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders the payment step: the list of payment methods and the panel of whichever provider the customer
/// selects.
/// </summary>
/// <remarks>
/// The method list is built from the payment providers actually registered on the tenant, not from a separate
/// configuration list. That way the page can never offer a method the framework cannot execute — the failure
/// mode that leaves a customer submitting a checkout that waits forever for a payment nothing can collect.
/// </remarks>
public sealed class PaymentStepCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly IEnumerable<ICheckoutPaymentProvider> _providers;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentStepCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="providers">The registered payment providers.</param>
    /// <param name="siteService">The site service used to read the configured default method.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentStepCheckoutFlowDisplayDriver(
        IEnumerable<ICheckoutPaymentProvider> providers,
        ISiteService siteService,
        IStringLocalizer<PaymentStepCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _providers = providers;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();

        var invoice = flow.Session.TryGet<CheckoutInvoice>(out var found) ? found : null;
        var obligations = invoice is null ? [] : CheckoutObligations.GetExpectedObligationIds(invoice);

        // Only providers that can actually settle what this invoice owes are offered. Offering one that
        // cannot would let the customer select it and only discover the mismatch when payment is refused.
        var eligible = CheckoutPaymentMethodSelector.GetEligible(_providers, obligations);

        var selected = CheckoutPaymentMethodSelector.ResolveDefault(eligible, settings.DefaultPaymentMethod);

        var model = new CheckoutPaymentMethodsViewModel
        {
            Flow = flow,
            RequiresPayment = obligations.Count > 0,
            SelectedProviderKey = selected,
            Methods = [.. eligible.Select(provider => new CheckoutPaymentMethodOption
            {
                Key = provider.Key,
                Title = provider.DisplayName,
                HasProcessor = CheckoutPaymentMethodSelector.IsGateway(provider),
                IsDefault = string.Equals(provider.Key, selected, StringComparison.OrdinalIgnoreCase),
                Description = CheckoutPaymentMethodSelector.IsGateway(provider)
                    ? S["Pay securely online and confirm immediately."].Value
                    : S["Confirm now and pay later."].Value,
            })],
        };

        // The provider panels are rendered by the view rather than composed here, so each provider's own
        // display driver owns its markup and this step stays provider-agnostic.
        return Initialize<CheckoutPaymentMethodsViewModel>("CheckoutPaymentMethods", viewModel =>
        {
            viewModel.Flow = model.Flow;
            viewModel.Methods = model.Methods;
            viewModel.RequiresPayment = model.RequiresPayment;
            viewModel.SelectedProviderKey = model.SelectedProviderKey;
        }).Location("Content:5");
    }
}
