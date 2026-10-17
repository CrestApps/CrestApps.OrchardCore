using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Shows the printable invoice of a transaction its owner pays themselves, to the customer who owns it and to the
/// administrators who manage transactions. Administrators also get the signed pay link, to send it by hand.
/// </summary>
/// <remarks>
/// The site theme shows the same invoice at <see cref="TransactionsConstants.RouteNames.Invoice"/>; both pages build it
/// through <see cref="ITransactionCustomerPageService"/>, so they apply the same ownership rules.
/// </remarks>
[Admin("transaction-invoices/{itemId}", TransactionsConstants.RouteNames.AdminInvoice)]
[CustomerAdminPage]
public sealed class TransactionInvoiceController : Controller
{
    private readonly ITransactionCustomerPageService _pages;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceController"/> class.
    /// </summary>
    /// <param name="pages">The service that builds the invoice and checks the viewer may see it.</param>
    public TransactionInvoiceController(ITransactionCustomerPageService pages)
    {
        _pages = pages;
    }

    /// <summary>
    /// Shows the invoice of the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    public async Task<IActionResult> Index(string itemId)
    {
        var result = await _pages.BuildInvoiceAsync(User, itemId, HttpContext.RequestAborted);

        return result.Status switch
        {
            TransactionCustomerPageStatus.Found => View(result.Model),
            TransactionCustomerPageStatus.Forbidden => Forbid(),
            _ => NotFound(),
        };
    }
}
