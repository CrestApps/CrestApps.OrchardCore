using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// Provides the customer statement of their own transactions and the online settlement entry point.
/// </summary>
[Admin]
public sealed class TransactionController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";

    private readonly ITransactionManager _transactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionController"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface confirmation messages.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionController(
        ITransactionManager transactionManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<TransactionController> htmlLocalizer,
        IStringLocalizer<TransactionController> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the current customer's transactions.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("my-transactions", "MyTransactions")]
    public async Task<IActionResult> Index(
        MyTransactionsOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var query = new TransactionQuery
        {
            OwnerId = userId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            Search = options.Search,
        };

        options.Status.ApplyTo(query);

        var result = await _transactionManager.PageAsync(pager.Page, pager.PageSize, query);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != TransactionStatusFilter.All)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status);
        }

        options.Statuses = TransactionStatusFilterExtensions.BuildFilterItems(options.Status, S);

        var model = new MyTransactionsViewModel
        {
            Transactions = result.Entries,
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        return View(model);
    }

    /// <summary>
    /// Preserves the statement filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("my-transactions", "MyTransactions")]
    public async Task<IActionResult> IndexFilterPost(MyTransactionsOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != TransactionStatusFilter.All)
        {
            routeValues.TryAdd(_optionsStatus, options.Status);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Displays one of the current customer's transactions.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var transaction = await GetOwnedTransactionAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        var model = new TransactionDetailViewModel
        {
            Transaction = transaction,
            CanManage = false,
        };

        return View(model);
    }

    /// <summary>
    /// Starts an online settlement checkout for one of the current customer's outstanding transactions.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Pay(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ViewOwnTransactions))
        {
            return Forbid();
        }

        var transaction = await GetOwnedTransactionAsync(itemId);

        if (transaction is null)
        {
            return NotFound();
        }

        if (transaction.OutstandingAmount <= 0m)
        {
            await _notifier.InformationAsync(H["This transaction has already been paid."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var checkoutSessionStore = HttpContext.RequestServices.GetService<ICheckoutSessionStore>();

        if (checkoutSessionStore is null)
        {
            await _notifier.WarningAsync(H["Online settlement is not available. Enable a checkout provider to pay online, or contact the site administrator."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var session = await checkoutSessionStore.NewAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId);

        await checkoutSessionStore.SaveAsync(session);

        await _notifier.InformationAsync(H["A settlement checkout was started. Complete the payment with a configured online payment provider to settle this transaction."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<Transaction> GetOwnedTransactionAsync(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var transaction = await _transactionManager.FindByIdAsync(itemId);

        if (transaction is null ||
            transaction.OwnerKind != CustomerOwnerKind.Authenticated ||
            !string.Equals(transaction.OwnerId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        return transaction;
    }
}
