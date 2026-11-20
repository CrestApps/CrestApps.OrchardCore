using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Names how money recorded by hand arrived, for every page and report that shows a payment recorded by hand.
/// </summary>
public static class OfflinePaymentMethodText
{
    /// <summary>
    /// Gets the localized name of one of <see cref="TransactionsConstants.OfflinePaymentMethods"/>, or the text staff
    /// typed.
    /// </summary>
    /// <param name="method">The stored payment method.</param>
    /// <param name="S">The localizer of the page or report that shows it.</param>
    public static string Name(string method, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return method switch
        {
            TransactionsConstants.OfflinePaymentMethods.Cash => S["Cash"],
            TransactionsConstants.OfflinePaymentMethods.Check => S["Check"],
            TransactionsConstants.OfflinePaymentMethods.BankTransfer => S["Bank transfer"],
            TransactionsConstants.OfflinePaymentMethods.CardTerminal => S["Card terminal"],
            TransactionsConstants.OfflinePaymentMethods.Other => S["Other"],
            _ => method,
        };
    }
}
