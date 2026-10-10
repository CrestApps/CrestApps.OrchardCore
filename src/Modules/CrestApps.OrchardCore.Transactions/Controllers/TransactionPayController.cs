using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// The page a signed pay link opens. It lets the owner of one transaction pay it without signing in, which is what a
/// customer created by an administrator (with no password) or a guest needs to pay an invoice. The link is the only
/// key: it names one transaction, cannot be altered, and expires, and the page shows nothing beyond that transaction.
/// </summary>
public sealed class TransactionPayController : Controller
{
    private readonly ITransactionPayLinkService _payLinks;
    private readonly ITransactionManager _transactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPayController"/> class.
    /// </summary>
    /// <param name="payLinks">The service that reads the signed pay links.</param>
    /// <param name="transactionManager">The transaction manager.</param>
    public TransactionPayController(ITransactionPayLinkService payLinks, ITransactionManager transactionManager)
    {
        _payLinks = payLinks;
        _transactionManager = transactionManager;
    }

    /// <summary>
    /// Shows the transaction a pay link names, with its invoice and a way to pay it.
    /// </summary>
    /// <param name="token">The signed token from the link.</param>
    [HttpGet("transactions/pay/{token}", Name = TransactionsConstants.RouteNames.Pay)]
    public async Task<IActionResult> Index(string token)
    {
        var transaction = await FindAsync(token);

        if (transaction is null)
        {
            return View("Invalid");
        }

        var invoiceBuilder = HttpContext.RequestServices.GetService<ITransactionInvoiceBuilder>();

        return View(new TransactionPayViewModel
        {
            Token = token,
            Transaction = transaction,
            Invoice = invoiceBuilder is null ? null : await invoiceBuilder.BuildAsync(transaction, HttpContext.RequestAborted),
            CanPay = IsPayable(transaction) && HttpContext.RequestServices.GetService<ICheckoutEngine>() is not null,
        });
    }

    /// <summary>
    /// Starts the checkout that pays the transaction a pay link names.
    /// </summary>
    /// <param name="token">The signed token from the link.</param>
    [HttpPost("transactions/pay/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string token)
    {
        var transaction = await FindAsync(token);

        if (transaction is null)
        {
            return View("Invalid");
        }

        var engine = HttpContext.RequestServices.GetService<ICheckoutEngine>();

        if (!IsPayable(transaction) || engine is null)
        {
            return RedirectToRoute(TransactionsConstants.RouteNames.Pay, new { token });
        }

        // The checkout belongs to whoever opened the link, a guest browser included; the settlement handler applies
        // what it collects to this transaction, whoever paid.
        var session = await engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
        });

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }

    private async Task<Transaction> FindAsync(string token)
    {
        var transactionId = _payLinks.GetTransactionId(token);

        return string.IsNullOrEmpty(transactionId) ? null : await _transactionManager.FindByIdAsync(transactionId);
    }

    private static bool IsPayable(Transaction transaction)
        => transaction.OutstandingAmount > 0m &&
            transaction.Status is not (TransactionStatus.Canceled or TransactionStatus.Paid or TransactionStatus.Refunded);
}
