using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Companies.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

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
    private readonly IShapeFactory _shapeFactory;

    public CompanyAccountController(
        CompanyService companies,
        IOrderStore orders,
        CompanyTermsService terms,
        IShapeFactory shapeFactory)
    {
        _companies = companies;
        _orders = orders;
        _terms = terms;
        _shapeFactory = shapeFactory;
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

        return View(await _shapeFactory.CreateAsync<CompanyAccountViewModel>("AccountCompany", model =>
        {
            model.Company = company;
            model.Buyer = buyer;
            model.Orders = orders;
            model.Owed = owed;
            model.Currency = currency;
        }));
    }
}
