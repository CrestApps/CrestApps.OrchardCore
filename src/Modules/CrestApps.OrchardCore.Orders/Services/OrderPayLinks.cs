using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Finds the link that pays what is still owed on an order, when Transactions is on and something is owed.
/// </summary>
internal static class OrderPayLinks
{
    public static async Task<string> GetPayUrlAsync(IServiceProvider services, Order order)
    {
        if (services.GetService<ITransactionStore>() is not { } transactions ||
            services.GetService<ITransactionPayLinkService>() is not { } payLinks)
        {
            return null;
        }

        var result = await transactions.PageAsync(1, 1, new TransactionQuery
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
            OutstandingOnly = true,
        });

        var outstanding = result.Entries.FirstOrDefault();

        return outstanding is null ? null : await payLinks.CreatePayUrlAsync(outstanding);
    }
}
