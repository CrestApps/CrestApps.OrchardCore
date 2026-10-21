using System.Globalization;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.PayLater.ViewModels;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// On an order placed with an offline method, while something is still owed: how to pay, for the buyer, and where
/// to record the payment, for the store.
/// </summary>
internal sealed class OrderOfflinePaymentDisplayDriver : DisplayDriver<Order>
{
    private readonly OfflinePaymentLookup _lookup;
    private readonly ISiteService _siteService;

    public OrderOfflinePaymentDisplayDriver(OfflinePaymentLookup lookup, ISiteService siteService)
    {
        _lookup = lookup;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrderConstants.DisplayTypes.DetailAdmin or OrderConstants.DisplayTypes.Detail) ||
            order.Status != OrderStatus.Paid ||
            await _lookup.FindAsync(order.CheckoutSessionId) is not { } found ||
            found.Balance is not { } balance ||
            balance.Status is not (TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid) ||
            balance.OutstandingAmount <= 0m)
        {
            return null;
        }

        var instructions = found.Provider switch
        {
            BankTransferCheckoutPaymentProvider => (await _siteService.GetSettingsAsync<BankTransferSettings>()).Instructions,
            CashOnDeliveryCheckoutPaymentProvider => (await _siteService.GetSettingsAsync<CashOnDeliverySettings>()).Instructions,
            _ => null,
        };

        var isAdmin = context.DisplayType == OrderConstants.DisplayTypes.DetailAdmin;

        return Initialize<OfflinePaymentMethodViewModel>(isAdmin ? "OrderOfflinePayment_DetailAdmin" : "OrderOfflinePayment_Detail", model =>
        {
            model.MethodKey = found.Provider.Key;
            model.MethodName = found.Provider.DisplayName;
            model.Instructions = instructions;
            model.Reference = string.IsNullOrEmpty(order.OrderNumber) ? order.ItemId : order.OrderNumber;
            model.AmountDue = string.Create(CultureInfo.CurrentCulture, $"{balance.Currency} {balance.OutstandingAmount:N2}");
            model.DueUtc = balance.DueUtc;
            model.TransactionId = isAdmin ? balance.ItemId : null;
        }).Location(isAdmin ? "Content:1" : "Content:2");
    }
}
