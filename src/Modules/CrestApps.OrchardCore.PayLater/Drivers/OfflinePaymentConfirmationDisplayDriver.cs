using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.PayLater.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// On the confirmation of a checkout paid by bank transfer, where to send the money and the reference to quote.
/// </summary>
public sealed class OfflinePaymentConfirmationDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly OfflinePaymentLookup _lookup;
    private readonly ISiteService _siteService;

    public OfflinePaymentConfirmationDisplayDriver(OfflinePaymentLookup lookup, ISiteService siteService)
    {
        _lookup = lookup;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(CheckoutFlow flow, BuildDisplayContext context)
    {
        if (context.DisplayType != "Confirmation" ||
            await _lookup.FindAsync(flow.Session.SessionId) is not { } found ||
            found.Provider is not BankTransferCheckoutPaymentProvider)
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<BankTransferSettings>();

        var reference = flow.Session is CheckoutSession session ? await _lookup.GetReferenceAsync(session) : flow.Session.SessionId;

        return Initialize<OfflinePaymentMethodViewModel>("BankTransferInstructions", model =>
        {
            model.Instructions = settings.Instructions;
            model.MethodName = found.Provider.DisplayName;
            model.MethodKey = found.Provider.Key;
            model.DueUtc = found.Balance?.DueUtc;
            model.Reference = reference;
        }).Location("Confirmation", "Content:1");
    }
}
