using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.PayLater.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// The Bank transfer and Cash on delivery panels on the checkout's payment step: what the buyer is agreeing to,
/// and how to pay.
/// </summary>
public sealed class OfflinePaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    private readonly ISiteService _siteService;

    public OfflinePaymentMethodDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => Combine(
            Initialize<OfflinePaymentMethodViewModel>("BankTransferCheckoutPaymentMethod", async model =>
            {
                model.Instructions = (await _siteService.GetSettingsAsync<BankTransferSettings>()).Instructions;
            }).Location("Content").OnGroup(BankTransferCheckoutPaymentProvider.ProcessorKey),
            Initialize<OfflinePaymentMethodViewModel>("CashOnDeliveryCheckoutPaymentMethod", async model =>
            {
                model.Instructions = (await _siteService.GetSettingsAsync<CashOnDeliverySettings>()).Instructions;
            }).Location("Content").OnGroup(CashOnDeliveryCheckoutPaymentProvider.ProcessorKey));
}
