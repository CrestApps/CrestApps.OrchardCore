using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Companies.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Companies.Controllers;

/// <summary>
/// The Company page of a buyer's storefront account: the company they buy for, its terms and locations, and its orders.
/// An admin sees every buyer's orders; a buyer sees their own.
/// </summary>
[Authorize]
public sealed class CompanyAccountController : Controller
{
    private const int RecentOrders = 50;

    private readonly CompanyService _companies;
    private readonly IOrderStore _orders;
    private readonly CompanyTermsService _terms;
    private readonly CompanyApprovalService _approvals;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public CompanyAccountController(
        CompanyService companies,
        IOrderStore orders,
        CompanyTermsService terms,
        CompanyApprovalService approvals,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<CompanyAccountController> htmlLocalizer)
    {
        _companies = companies;
        _orders = orders;
        _terms = terms;
        _approvals = approvals;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("account/company", Name = "AccountCompany")]
    public async Task<IActionResult> Index()
    {
        var (company, buyer) = await _companies.GetCurrentAsync(HttpContext.RequestAborted);

        if (company is null || buyer is null)
        {
            return NotFound();
        }

        var (_, orders) = await _orders.PageAsync(new OrderQuery
        {
            CompanyId = company.ItemId,
            UserId = buyer.Role == CompanyBuyerRole.Admin ? null : buyer.UserId,
        }, 0, RecentOrders, HttpContext.RequestAborted);
        var (owed, currency) = await _terms.GetOwedAsync(company, HttpContext.RequestAborted);
        var approvals = buyer.Role == CompanyBuyerRole.Admin ? await ListApprovalsAsync(_orders, company.ItemId, HttpContext.RequestAborted) : [];
        decimal? spent = company.MonthlyBudget is null ? null : await _approvals.GetSpentThisMonthAsync(company, currency, cancellationToken: HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<CompanyAccountViewModel>("AccountCompany", model =>
        {
            model.Company = company;
            model.Buyer = buyer;
            model.Orders = orders;
            model.Owed = owed;
            model.Currency = currency;
            model.Approvals = approvals;
            model.SpentThisMonth = spent;
        }));
    }

    [HttpPost("account/company/approvals/{orderId}", Name = "AccountCompanyDecide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(string orderId, bool approve, string note)
    {
        var (company, buyer) = await _companies.GetCurrentAsync(HttpContext.RequestAborted);

        if (company is null || buyer?.Role != CompanyBuyerRole.Admin ||
            await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order || order.CompanyId != company.ItemId)
        {
            return NotFound();
        }

        if (await _approvals.DecideAsync(order, approve, buyer.UserName ?? buyer.Email, note, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(approve ? H["You approved order {0}. The buyer can pay it now.", order.OrderNumber] : H["You rejected order {0}.", order.OrderNumber]);
        }

        return RedirectToRoute("AccountCompany");
    }

    /// <summary>
    /// Returns a company's orders waiting for an admin's approval, oldest first.
    /// </summary>
    internal static async Task<IReadOnlyList<Order>> ListApprovalsAsync(IOrderStore orders, string companyId, CancellationToken cancellationToken)
    {
        var (_, open) = await orders.PageAsync(new OrderQuery
        {
            CompanyId = companyId,
            Statuses = [OrderStatus.Draft, OrderStatus.AwaitingPayment],
        }, 0, 200, cancellationToken);

        return [.. open.Where(order => order.Approval?.Status == OrderApprovalStatus.Pending).OrderBy(order => order.Approval.RequestedUtc)];
    }
}
