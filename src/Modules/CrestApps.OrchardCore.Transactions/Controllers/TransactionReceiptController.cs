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
/// Shows the printable receipt for one payment applied to a transaction, to the customer who owns it and to the
/// administrators who manage transactions.
/// </summary>
[Admin("transaction-receipts/{itemId}/{paymentId}", "TransactionReceipt")]
[Feature(TransactionsConstants.Features.Receipts)]
public sealed class TransactionReceiptController : Controller
{
    private readonly ITransactionManager _transactionManager;
    private readonly ITransactionReceiptBuilder _receiptBuilder;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReceiptController"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="receiptBuilder">The receipt builder.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TransactionReceiptController(
        ITransactionManager transactionManager,
        ITransactionReceiptBuilder receiptBuilder,
        IAuthorizationService authorizationService)
    {
        _transactionManager = transactionManager;
        _receiptBuilder = receiptBuilder;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Shows the receipt for the payment <paramref name="paymentId"/> on the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="paymentId">The identifier of the timeline event that recorded the payment.</param>
    public async Task<IActionResult> Index(string itemId, string paymentId)
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(paymentId))
        {
            return NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions);

        if (!canManage && !await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        // A customer only ever sees a receipt for their own money. Answering "not found" rather than "forbidden"
        // for someone else's transaction keeps the identifiers from being probed.
        if (transaction is null || (!canManage && !IsOwnedByCurrentUser(transaction)))
        {
            return NotFound();
        }

        var payment = transaction.Events.FirstOrDefault(evt =>
            evt.Type == TransactionEventType.PaymentRecorded &&
            string.Equals(evt.Id, paymentId, StringComparison.Ordinal));

        if (payment is null)
        {
            return NotFound();
        }

        var receipt = await _receiptBuilder.BuildAsync(transaction, payment, HttpContext.RequestAborted);

        if (receipt is null)
        {
            return NotFound();
        }

        return View(new TransactionReceiptViewModel
        {
            Transaction = transaction,
            Receipt = receipt,
            IsAdministrator = canManage,
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
