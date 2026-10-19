using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Controllers;

/// <summary>
/// The customer pages in the site theme: the statement of the customer's own transactions, the printable invoice of
/// one, and the printable receipt of a payment. They show the same documents as the customer pages in the admin, with
/// the same ownership rules, so a customer who never uses the admin can see and print them.
/// </summary>
/// <remarks>
/// Each page renders a shape (<see cref="TransactionsConstants.Shapes"/>) that a theme can override; the statement
/// page's filter and statement together are the <c>AccountTransactions</c> shape.
/// </remarks>
public sealed class MyTransactionsController : Controller
{
    private readonly ITransactionCustomerPageService _pages;
    private readonly IShapeFactory _shapeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTransactionsController"/> class.
    /// </summary>
    /// <param name="pages">The service that builds the customer's documents and checks they may see them.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    public MyTransactionsController(
        ITransactionCustomerPageService pages,
        IShapeFactory shapeFactory)
    {
        _pages = pages;
        _shapeFactory = shapeFactory;
    }

    /// <summary>
    /// Shows the current customer's statement.
    /// </summary>
    /// <param name="search">The title search, if any.</param>
    /// <param name="outstanding">Whether only the transactions with a balance still owed are listed.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    [HttpGet("my-transactions", Name = TransactionsConstants.RouteNames.Statement)]
    public async Task<IActionResult> Index(
        string search,
        bool outstanding,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);

        var result = await _pages.BuildStatementAsync(
            User,
            new TransactionStatementRequest
            {
                Page = pager.Page,
                PageSize = pager.PageSize,
                Search = search,
                OutstandingOnly = outstanding,
            },
            HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ToActionResult(result.Status);
        }

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(search))
        {
            routeData.Values.TryAdd(nameof(search), search);
        }

        if (outstanding)
        {
            routeData.Values.TryAdd(nameof(outstanding), true);
        }

        result.Model.Pager = await _shapeFactory.PagerAsync(pager, result.Model.TotalCount, routeData);

        var statement = await _shapeFactory.CreateAsync<TransactionStatementViewModel>(TransactionsConstants.Shapes.Statement, result.Model.CopyTo);

        return View(await _shapeFactory.CreateAsync<MyTransactionsPageViewModel>("AccountTransactions", model =>
        {
            model.Search = search;
            model.Outstanding = outstanding;
            model.Statement = statement;
        }));
    }

    /// <summary>
    /// Shows the invoice of the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    [HttpGet("my-transactions/invoices/{itemId}", Name = TransactionsConstants.RouteNames.Invoice)]
    public async Task<IActionResult> Invoice(string itemId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await _pages.BuildInvoiceAsync(User, itemId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ToActionResult(result.Status);
        }

        return View(await _shapeFactory.CreateAsync<TransactionInvoiceViewModel>(TransactionsConstants.Shapes.Invoice, result.Model.CopyTo));
    }

    /// <summary>
    /// Shows the receipt for the payment <paramref name="paymentId"/> on the transaction <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">The transaction identifier.</param>
    /// <param name="paymentId">The identifier of the timeline event that recorded the payment.</param>
    [HttpGet("my-transactions/receipts/{itemId}/{paymentId}", Name = TransactionsConstants.RouteNames.Receipt)]
    public async Task<IActionResult> Receipt(string itemId, string paymentId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await _pages.BuildReceiptAsync(User, itemId, paymentId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ToActionResult(result.Status);
        }

        return View(await _shapeFactory.CreateAsync<TransactionReceiptViewModel>(TransactionsConstants.Shapes.Receipt, result.Model.CopyTo));
    }

    private IActionResult ToActionResult(TransactionCustomerPageStatus status)
        => status == TransactionCustomerPageStatus.Forbidden
            ? Forbid()
            : NotFound();
}
