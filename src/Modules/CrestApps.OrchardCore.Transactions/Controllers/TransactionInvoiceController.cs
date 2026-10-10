using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Shows the printable invoice of a transaction its owner pays themselves, to the customer who owns it and to the
/// administrators who manage transactions. Administrators also get the signed pay link, to send it by hand.
/// </summary>
[Admin("transaction-invoices/{itemId}", "TransactionInvoice")]
[Feature(TransactionsConstants.Features.Receipts)]
public sealed class TransactionInvoiceController : Controller
{
    private readonly ITransactionManager _transactionManager;
    private readonly ITransactionInvoiceBuilder _invoiceBuilder;
    private readonly ITransactionPayLinkService _payLinks;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceController"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="invoiceBuilder">The invoice builder.</param>
    /// <param name="payLinks">The service that creates signed pay links.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TransactionInvoiceController(
        ITransactionManager transactionManager,
        ITransactionInvoiceBuilder invoiceBuilder,
        ITransactionPayLinkService payLinks,
        IAuthorizationService authorizationService)
    {
        _transactionManager = transactionManager;
        _invoiceBuilder = invoiceBuilder;
        _payLinks = payLinks;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Shows the invoice of the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    public async Task<IActionResult> Index(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions);

        if (!canManage && !await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        // A customer only ever sees their own invoice. "Not found" rather than "forbidden" keeps identifiers from being
        // probed. An invoice exists only once the customer was sent one.
        if (transaction is null || string.IsNullOrEmpty(transaction.InvoiceNumber) || (!canManage && !IsOwnedByCurrentUser(transaction)))
        {
            return NotFound();
        }

        var payable = transaction.OutstandingAmount > 0m &&
            transaction.Status is not (TransactionStatus.Canceled or TransactionStatus.Paid or TransactionStatus.Refunded);

        return View(new TransactionInvoiceViewModel
        {
            Transaction = transaction,
            Invoice = await _invoiceBuilder.BuildAsync(transaction, HttpContext.RequestAborted),
            IsAdministrator = canManage,
            PayUrl = canManage && payable ? await _payLinks.CreatePayUrlAsync(transaction, HttpContext.RequestAborted) : null,
        });
    }

    private bool IsOwnedByCurrentUser(Transaction transaction)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(userId) &&
            transaction.OwnerKind == CustomerOwnerKind.Authenticated &&
            string.Equals(transaction.OwnerId, userId, StringComparison.Ordinal);
    }
}
