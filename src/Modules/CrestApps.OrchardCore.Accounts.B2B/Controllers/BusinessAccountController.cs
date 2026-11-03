using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Accounts.B2B.Controllers;

/// <summary>
/// The Business account page of a buyer's storefront account: the account they buy for, its terms and locations, and its orders.
/// An admin sees every buyer's orders; a buyer sees their own.
/// </summary>
[Authorize]
public sealed class BusinessAccountController : Controller
{
    private const int RecentOrders = 50;

    private readonly B2BAccountService _accounts;
    private readonly IOrderStore _orders;
    private readonly AccountTermsService _terms;
    private readonly B2BApprovalService _approvals;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public BusinessAccountController(
        B2BAccountService accounts,
        IOrderStore orders,
        AccountTermsService terms,
        B2BApprovalService approvals,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<BusinessAccountController> htmlLocalizer)
    {
        _accounts = accounts;
        _orders = orders;
        _terms = terms;
        _approvals = approvals;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("account/business", Name = "BusinessAccount")]
    public async Task<IActionResult> Index()
    {
        var (account, buyer) = await _accounts.GetCurrentAsync(HttpContext.RequestAborted);

        if (account is null || buyer is null)
        {
            return NotFound();
        }

        var (_, orders) = await _orders.PageAsync(new OrderQuery
        {
            AccountId = account.AccountId,
            UserId = buyer.Role == AccountMemberRole.Admin ? null : buyer.UserId,
        }, 0, RecentOrders, HttpContext.RequestAborted);
        var (owed, currency) = await _terms.GetOwedAsync(account, HttpContext.RequestAborted);
        var approvals = buyer.Role == AccountMemberRole.Admin ? await ListApprovalsAsync(_orders, account.AccountId, HttpContext.RequestAborted) : [];
        decimal? spent = account.MonthlyBudget is null ? null : await _approvals.GetSpentThisMonthAsync(account, currency, cancellationToken: HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<BusinessAccountViewModel>("BusinessAccount", model =>
        {
            model.Account = account;
            model.Buyer = buyer;
            model.Orders = orders;
            model.Owed = owed;
            model.Currency = currency;
            model.Approvals = approvals;
            model.SpentThisMonth = spent;
        }));
    }

    [HttpPost("account/business/approvals/{orderId}", Name = "BusinessAccountDecide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(string orderId, bool approve, string note)
    {
        var (account, buyer) = await _accounts.GetCurrentAsync(HttpContext.RequestAborted);

        if (account is null || buyer?.Role != AccountMemberRole.Admin ||
            await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order || order.AccountId != account.AccountId)
        {
            return NotFound();
        }

        if (await _approvals.DecideAsync(order, approve, buyer.UserName ?? buyer.Email, note, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(approve ? H["You approved order {0}. The buyer can pay it now.", order.OrderNumber] : H["You rejected order {0}.", order.OrderNumber]);
        }

        return RedirectToRoute("BusinessAccount");
    }

    /// <summary>
    /// Returns an account's orders waiting for an admin's approval, oldest first.
    /// </summary>
    internal static async Task<IReadOnlyList<Order>> ListApprovalsAsync(IOrderStore orders, string accountId, CancellationToken cancellationToken)
    {
        var (_, open) = await orders.PageAsync(new OrderQuery
        {
            AccountId = accountId,
            Statuses = [OrderStatus.Draft, OrderStatus.AwaitingPayment],
        }, 0, 200, cancellationToken);

        return [.. open.Where(order => order.Approval?.Status == OrderApprovalStatus.Pending).OrderBy(order => order.Approval.RequestedUtc)];
    }
}
