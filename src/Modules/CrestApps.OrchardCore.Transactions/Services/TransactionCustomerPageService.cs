using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionCustomerPageService"/>. The customer pages in the admin and in the site theme
/// both build their documents here, so they apply exactly the same ownership rules.
/// </summary>
public sealed class TransactionCustomerPageService : ITransactionCustomerPageService
{
    private readonly ITransactionManager _transactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITransactionPayLinkService _payLinks;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCustomerPageService"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="payLinks">The service that creates signed pay links.</param>
    /// <param name="httpContextAccessor">The accessor for the current request, used to link the statement's documents.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="serviceProvider">
    /// The service provider the optional invoice and receipt builders are resolved from. They are registered only
    /// while the Receipts module is enabled.
    /// </param>
    public TransactionCustomerPageService(
        ITransactionManager transactionManager,
        IAuthorizationService authorizationService,
        ITransactionPayLinkService payLinks,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IServiceProvider serviceProvider)
    {
        _transactionManager = transactionManager;
        _authorizationService = authorizationService;
        _payLinks = payLinks;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<TransactionCustomerPageResult<TransactionStatementViewModel>> BuildStatementAsync(
        ClaimsPrincipal user,
        TransactionStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);

        if (!await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ViewOwnTransactions))
        {
            return TransactionCustomerPageResult<TransactionStatementViewModel>.Forbidden();
        }

        var userId = GetUserId(user);

        if (string.IsNullOrEmpty(userId))
        {
            return TransactionCustomerPageResult<TransactionStatementViewModel>.Forbidden();
        }

        var query = new TransactionQuery
        {
            OwnerId = userId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            Search = request.Search,
            OutstandingOnly = request.OutstandingOnly,
        };

        var page = Math.Max(1, request.Page);
        var pageSize = request.PageSize > 0
            ? request.PageSize
            : TransactionStatementRequest.DefaultPageSize;

        var result = await _transactionManager.PageAsync(page, pageSize, query, cancellationToken);

        var showInvoices = _serviceProvider.GetService<ITransactionInvoiceBuilder>() is not null;
        var showReceipts = _serviceProvider.GetService<ITransactionReceiptBuilder>() is not null;
        var entries = new List<TransactionStatementEntry>();

        foreach (var transaction in result.Entries)
        {
            entries.Add(new TransactionStatementEntry
            {
                Transaction = transaction,
                PayUrl = TransactionStateMachine.CanRecordPayment(transaction)
                    ? await _payLinks.CreatePayUrlAsync(transaction, cancellationToken)
                    : null,
                InvoiceUrl = showInvoices && !string.IsNullOrEmpty(transaction.InvoiceNumber)
                    ? GetPath(TransactionsConstants.RouteNames.Invoice, new RouteValueDictionary
                    {
                        ["itemId"] = transaction.ItemId,
                    })
                    : null,
                Receipts = showReceipts
                    ? BuildReceiptLinks(transaction)
                    : [],
            });
        }

        return TransactionCustomerPageResult<TransactionStatementViewModel>.Found(new TransactionStatementViewModel
        {
            Entries = entries,
            TotalCount = result.Count,
            Search = request.Search,
            OutstandingOnly = request.OutstandingOnly,
        });
    }

    /// <inheritdoc/>
    public async Task<TransactionCustomerPageResult<TransactionInvoiceViewModel>> BuildInvoiceAsync(
        ClaimsPrincipal user,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // The invoice is laid out by the Receipts module, so without it there is no invoice to show.
        if (string.IsNullOrEmpty(transactionId) ||
            _serviceProvider.GetService<ITransactionInvoiceBuilder>() is not { } invoiceBuilder)
        {
            return TransactionCustomerPageResult<TransactionInvoiceViewModel>.NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactions);

        if (!canManage && !await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ViewOwnTransactions))
        {
            return TransactionCustomerPageResult<TransactionInvoiceViewModel>.Forbidden();
        }

        var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

        // A customer only ever sees their own invoice. "Not found" rather than "forbidden" keeps identifiers from being
        // probed. An invoice exists only once the customer was sent one.
        if (transaction is null ||
            string.IsNullOrEmpty(transaction.InvoiceNumber) ||
            (!canManage && !IsOwnedBy(user, transaction)))
        {
            return TransactionCustomerPageResult<TransactionInvoiceViewModel>.NotFound();
        }

        return TransactionCustomerPageResult<TransactionInvoiceViewModel>.Found(new TransactionInvoiceViewModel
        {
            Transaction = transaction,
            Invoice = await invoiceBuilder.BuildAsync(transaction, cancellationToken),
            IsAdministrator = canManage,
            PayUrl = TransactionStateMachine.CanRecordPayment(transaction)
                ? await _payLinks.CreatePayUrlAsync(transaction, cancellationToken)
                : null,
        });
    }

    /// <inheritdoc/>
    public async Task<TransactionCustomerPageResult<TransactionReceiptViewModel>> BuildReceiptAsync(
        ClaimsPrincipal user,
        string transactionId,
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // The receipt is built by the Receipts module, so without it there is no receipt to show.
        if (string.IsNullOrEmpty(transactionId) ||
            string.IsNullOrEmpty(paymentId) ||
            _serviceProvider.GetService<ITransactionReceiptBuilder>() is not { } receiptBuilder)
        {
            return TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactions);

        if (!canManage && !await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ViewOwnTransactions))
        {
            return TransactionCustomerPageResult<TransactionReceiptViewModel>.Forbidden();
        }

        var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

        // A customer only ever sees a receipt for their own money. Answering "not found" rather than "forbidden"
        // for someone else's transaction keeps the identifiers from being probed.
        if (transaction is null || (!canManage && !IsOwnedBy(user, transaction)))
        {
            return TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound();
        }

        var payment = transaction.Events.FirstOrDefault(evt =>
            evt.Type == TransactionEventType.PaymentRecorded &&
            string.Equals(evt.Id, paymentId, StringComparison.Ordinal));

        if (payment is null)
        {
            return TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound();
        }

        var receipt = await receiptBuilder.BuildAsync(transaction, payment, cancellationToken);

        if (receipt is null)
        {
            return TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound();
        }

        return TransactionCustomerPageResult<TransactionReceiptViewModel>.Found(new TransactionReceiptViewModel
        {
            Transaction = transaction,
            Payment = payment,
            Receipt = receipt,
            IsAdministrator = canManage,
        });
    }

    /// <summary>
    /// Returns whether <paramref name="user"/> is the signed-up customer who owns <paramref name="transaction"/>. A guest
    /// transaction is never owned by an account, even one that happens to share its identifier.
    /// </summary>
    /// <param name="user">The viewer.</param>
    /// <param name="transaction">The transaction.</param>
    private static bool IsOwnedBy(ClaimsPrincipal user, Transaction transaction)
    {
        var userId = GetUserId(user);

        return !string.IsNullOrEmpty(userId) &&
            transaction.OwnerKind == CustomerOwnerKind.Authenticated &&
            string.Equals(transaction.OwnerId, userId, StringComparison.Ordinal);
    }

    private static string GetUserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    private List<TransactionStatementReceipt> BuildReceiptLinks(Transaction transaction)
    {
        var receipts = new List<TransactionStatementReceipt>();

        foreach (var payment in transaction.Events.OrderBy(evt => evt.CreatedUtc))
        {
            if (payment.Type != TransactionEventType.PaymentRecorded ||
                string.IsNullOrEmpty(payment.Id) ||
                payment.Amount is not > 0m)
            {
                continue;
            }

            receipts.Add(new TransactionStatementReceipt
            {
                Payment = payment,
                Url = GetPath(TransactionsConstants.RouteNames.Receipt, new RouteValueDictionary
                {
                    ["itemId"] = transaction.ItemId,
                    ["paymentId"] = payment.Id,
                }),
            });
        }

        return receipts;
    }

    private string GetPath(string routeName, RouteValueDictionary values)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetPathByRouteValues(httpContext, routeName, values);
    }
}
