using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Shows the printable receipt for one payment applied to a transaction, to the customer who owns it and to the
/// administrators who manage transactions.
/// </summary>
/// <remarks>
/// The site theme shows the same receipt at <see cref="TransactionsConstants.RouteNames.Receipt"/>; both pages build
/// it through <see cref="ITransactionCustomerPageService"/>, so they apply the same ownership rules.
/// </remarks>
[Admin("transaction-receipts/{itemId}/{paymentId}", TransactionsConstants.RouteNames.AdminReceipt)]
[CustomerAdminPage]
public sealed class TransactionReceiptController : Controller
{
    private readonly ITransactionCustomerPageService _pages;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReceiptController"/> class.
    /// </summary>
    /// <param name="pages">The service that builds the receipt and checks the viewer may see it.</param>
    public TransactionReceiptController(ITransactionCustomerPageService pages)
    {
        _pages = pages;
    }

    /// <summary>
    /// Shows the receipt for the payment <paramref name="paymentId"/> on the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="paymentId">The identifier of the timeline event that recorded the payment.</param>
    public async Task<IActionResult> Index(string itemId, string paymentId)
    {
        var result = await _pages.BuildReceiptAsync(User, itemId, paymentId, HttpContext.RequestAborted);

        return result.Status switch
        {
            TransactionCustomerPageStatus.Found => View(result.Model),
            TransactionCustomerPageStatus.Forbidden => Forbid(),
            _ => NotFound(),
        };
    }
}
