using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.PayLater.ViewModels;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// On the confirmation of a checkout paid offline, how to pay: for a bank transfer, where to send the money and the
/// reference to quote; for Pay Later, what is owed, when it is due and where the buyer pays it.
/// </summary>
public sealed class OfflinePaymentConfirmationDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly OfflinePaymentLookup _lookup;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;

    public OfflinePaymentConfirmationDisplayDriver(OfflinePaymentLookup lookup, ISiteService siteService, IServiceProvider serviceProvider)
    {
        _lookup = lookup;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(CheckoutFlow flow, BuildDisplayContext context)
    {
        if (context.DisplayType != "Confirmation" ||
            await _lookup.FindAsync(flow.Session.SessionId) is not { } found)
        {
            return null;
        }

        if (found.Provider is PayLaterCheckoutPaymentProvider)
        {
            var balance = found.Balance;

            // A signed-in buyer pays from their statement; a guest is sent the invoice with a link that pays it.
            var statementUrl = balance is { OwnerKind: not CustomerOwnerKind.Guest } &&
                _serviceProvider.GetService<ITransactionCustomerLinkService>() is { } links
                    ? await links.GetStatementUrlAsync()
                    : null;

            return Initialize<OfflinePaymentMethodViewModel>("PayLaterConfirmation", model =>
            {
                model.MethodName = found.Provider.DisplayName;
                model.MethodKey = found.Provider.Key;
                model.TransactionId = balance?.ItemId;
                model.DueUtc = balance?.DueUtc;
                model.AmountDue = balance is null ? null : $"{balance.Currency} {CurrencyScale.Format(balance.OutstandingAmount, balance.Currency)}";
                model.StatementUrl = statementUrl;
            }).Location("Confirmation", "Content:1");
        }

        if (found.Provider is not BankTransferCheckoutPaymentProvider)
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
