using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Companies.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Companies.Controllers;

/// <summary>
/// Commerce › Companies: the businesses the store sells to, their buyers, locations and terms.
/// </summary>
[Admin("companies/{action}/{itemId?}", "Companies{action}")]
public sealed class CompaniesAdminController : Controller
{
    // Empty rows the editor offers to add buyers and locations.
    private const int SpareRows = 2;

    private readonly ICompanyStore _companies;
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CompaniesAdminController(
        ICompanyStore companies,
        UserManager<IUser> userManager,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<CompaniesAdminController> htmlLocalizer,
        IStringLocalizer<CompaniesAdminController> stringLocalizer)
    {
        _companies = companies;
        _userManager = userManager;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("companies", "Companies")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, companies) = await _companies.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);

        return View(new CompaniesIndexViewModel
        {
            Search = q,
            List = new AdminListContext<Company>
            {
                Source = companies,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new CompanyEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(CompanyEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        var company = new Company { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        if (!await ApplyAsync(model, company))
        {
            return View("Edit", await PopulateAsync(model));
        }

        await _companies.CreateAsync(company);
        await _notifier.SuccessAsync(H["The company was added. Its buyers buy for it once they sign in."]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _companies.FindByIdAsync(itemId) is not { } company)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new CompanyEditViewModel
        {
            Name = company.Name,
            TaxId = company.TaxId,
            BillingEmail = company.BillingEmail,
            IsEnabled = company.IsEnabled,
            NetDays = company.Terms?.NetDays,
            DepositPercent = company.Terms?.DepositPercent ?? 0m,
            CreditLimit = company.Terms?.CreditLimit,
            RequirePurchaseOrder = company.Terms?.RequirePurchaseOrder == true,
            MonthlyBudget = company.MonthlyBudget,
            PriceListId = company.PriceListId,
            Buyers = [.. company.Buyers.Select(buyer => new CompanyBuyerRowViewModel { User = buyer.Email ?? buyer.UserName, Role = buyer.Role, OrderLimit = buyer.OrderLimit })],
            Locations = [.. company.Locations.Select(location => new CompanyLocationRowViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                AddressLine1 = location.Address?.AddressLine1,
                City = location.Address?.City,
                Region = location.Address?.Region,
                PostalCode = location.Address?.PostalCode,
                Country = location.Address?.Country,
            })],
        }, company.ItemId));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, CompanyEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _companies.FindByIdAsync(itemId) is not { } company)
        {
            return NotFound();
        }

        if (!await ApplyAsync(model, company))
        {
            return View(await PopulateAsync(model, company.ItemId));
        }

        company.ModifiedUtc = _clock.UtcNow;
        await _companies.UpdateAsync(company);
        await _notifier.SuccessAsync(H["The company was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _companies.FindByIdAsync(itemId) is not { } company)
        {
            return NotFound();
        }

        await _companies.DeleteAsync(company);
        await _notifier.SuccessAsync(H["The company was deleted. Its buyers now buy for themselves; its orders keep it."]);

        return RedirectToAction(nameof(Index));
    }

    // Checks the form and copies it onto the company. Buyers are found by their user name or email, and a user buys
    // for one company only.
    private async Task<bool> ApplyAsync(CompanyEditViewModel model, Company company)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the company."]);
        }

        if (model.NetDays is < 0 or > 365)
        {
            ModelState.AddModelError(nameof(model.NetDays), S["Enter the days an invoice is due in, from 0 to 365, or leave it blank for no terms."]);
        }

        if (model.DepositPercent is < 0m or > 100m)
        {
            ModelState.AddModelError(nameof(model.DepositPercent), S["Enter a deposit from 0 to 100 percent."]);
        }

        if (model.CreditLimit is < 0m || model.MonthlyBudget is < 0m)
        {
            ModelState.AddModelError(nameof(model.CreditLimit), S["A credit limit or budget can't be negative."]);
        }

        var buyers = new List<CompanyBuyer>();

        foreach (var row in (model.Buyers ?? []).Where(row => !string.IsNullOrWhiteSpace(row.User)))
        {
            var name = row.User.Trim();
            var user = await _userManager.FindByEmailAsync(name) ?? await _userManager.FindByNameAsync(name);

            if (user is null)
            {
                ModelState.AddModelError(nameof(model.Buyers), S["There's no user {0}. Add the buyer as a user first.", name]);

                continue;
            }

            var userId = await _userManager.GetUserIdAsync(user);

            if (buyers.Any(buyer => buyer.UserId == userId))
            {
                continue;
            }

            if (await _companies.FindByBuyerAsync(userId, HttpContext.RequestAborted) is { } other && other.ItemId != company.ItemId)
            {
                ModelState.AddModelError(nameof(model.Buyers), S["{0} already buys for {1}.", name, other.Name]);

                continue;
            }

            buyers.Add(new CompanyBuyer
            {
                UserId = userId,
                UserName = await _userManager.GetUserNameAsync(user),
                Email = await _userManager.GetEmailAsync(user),
                Role = row.Role,
                OrderLimit = row.OrderLimit is > 0m ? row.OrderLimit : null,
            });
        }

        var locations = new List<CompanyLocation>();

        foreach (var row in (model.Locations ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Name) || !string.IsNullOrWhiteSpace(row.AddressLine1)))
        {
            if (string.IsNullOrWhiteSpace(row.AddressLine1) || string.IsNullOrWhiteSpace(row.City) || row.Country?.Trim().Length != 2)
            {
                ModelState.AddModelError(nameof(model.Locations), S["Give each location its street, city and two-letter country code."]);

                continue;
            }

            locations.Add(new CompanyLocation
            {
                LocationId = string.IsNullOrEmpty(row.LocationId) ? IdGenerator.GenerateId() : row.LocationId,
                Name = string.IsNullOrWhiteSpace(row.Name) ? row.City.Trim() : row.Name.Trim(),
                Address = new Address
                {
                    Name = model.Name?.Trim(),
                    Company = model.Name?.Trim(),
                    AddressLine1 = row.AddressLine1.Trim(),
                    City = row.City.Trim(),
                    Region = string.IsNullOrWhiteSpace(row.Region) ? null : row.Region.Trim().ToUpperInvariant(),
                    PostalCode = string.IsNullOrWhiteSpace(row.PostalCode) ? null : row.PostalCode.Trim(),
                    Country = row.Country.Trim().ToUpperInvariant(),
                },
            });
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        company.Name = model.Name.Trim();
        company.TaxId = string.IsNullOrWhiteSpace(model.TaxId) ? null : model.TaxId.Trim();
        company.BillingEmail = string.IsNullOrWhiteSpace(model.BillingEmail) ? null : model.BillingEmail.Trim();
        company.IsEnabled = model.IsEnabled;
        company.Terms = new CompanyTerms
        {
            NetDays = model.NetDays,
            DepositPercent = model.DepositPercent,
            CreditLimit = model.CreditLimit,
            RequirePurchaseOrder = model.RequirePurchaseOrder,
        };
        company.MonthlyBudget = model.MonthlyBudget;
        company.PriceListId = string.IsNullOrWhiteSpace(model.PriceListId) ? null : model.PriceListId;
        company.Buyers = buyers;
        company.Locations = locations;

        return true;
    }

    private async Task<CompanyEditViewModel> PopulateAsync(CompanyEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        for (var i = 0; i < SpareRows; i++)
        {
            model.Buyers.Add(new CompanyBuyerRowViewModel());
            model.Locations.Add(new CompanyLocationRowViewModel());
        }

        return model;
    }
}
