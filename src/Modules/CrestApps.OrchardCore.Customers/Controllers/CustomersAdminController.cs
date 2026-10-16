using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// Lists the store's customers, and lets the people who manage them add and edit them.
/// </summary>
public sealed class CustomersAdminController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string OptionsFilter = "Options.Filter";

    private static readonly EmailAddressAttribute _emailAddress = new();

    private readonly ICustomerStore _store;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public CustomersAdminController(
        ICustomerStore store,
        IUserService userService,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IClock clock,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<CustomersAdminController> stringLocalizer,
        IHtmlLocalizer<CustomersAdminController> htmlLocalizer)
    {
        _store = store;
        _userService = userService;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _clock = clock;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [Admin("customers", "CustomersIndex")]
    public async Task<IActionResult> Index(CustomersIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ViewCustomers))
        {
            return Forbid();
        }

        options ??= new CustomersIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var query = new CustomerQuery { Search = options.Search };

        switch (options.Filter)
        {
            case CustomerListFilter.Active:
                query.Status = CustomerStatus.Active;
                break;
            case CustomerListFilter.WithAccount:
                query.Status = CustomerStatus.Active;
                query.HasAccount = true;
                break;
            case CustomerListFilter.Guests:
                query.Status = CustomerStatus.Active;
                query.HasAccount = false;
                break;
            case CustomerListFilter.Businesses:
                query.Status = CustomerStatus.Active;
                query.Kind = CustomerKind.Business;
                break;
            case CustomerListFilter.Disabled:
                query.Status = CustomerStatus.Disabled;
                break;
        }

        var (count, customers) = await _store.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Filters =
        [
            new SelectListItem(S["Active"], nameof(CustomerListFilter.Active), options.Filter == CustomerListFilter.Active),
            new SelectListItem(S["With an account"], nameof(CustomerListFilter.WithAccount), options.Filter == CustomerListFilter.WithAccount),
            new SelectListItem(S["Guests"], nameof(CustomerListFilter.Guests), options.Filter == CustomerListFilter.Guests),
            new SelectListItem(S["Businesses"], nameof(CustomerListFilter.Businesses), options.Filter == CustomerListFilter.Businesses),
            new SelectListItem(S["Disabled"], nameof(CustomerListFilter.Disabled), options.Filter == CustomerListFilter.Disabled),
            new SelectListItem(S["All customers"], nameof(CustomerListFilter.All), options.Filter == CustomerListFilter.All),
        ];

        var routeData = new RouteData();

        foreach (var (key, value) in FilterRouteValues(options, null))
        {
            routeData.Values.TryAdd(key, value);
        }

        return View(new CustomersIndexViewModel
        {
            Options = options,
            Customers = customers,
            TotalCount = count,
            CanManage = await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers),
            Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
        });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("customers", "CustomersIndex")]
    public async Task<IActionResult> IndexFilterPost(CustomersIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ViewCustomers))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), FilterRouteValues(options, pagerParameters));
    }

    [Admin("customers/create", "CustomersCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        return View("Edit", await BuildEditorAsync(new CustomerEditViewModel { IsNew = true, CanManage = true }));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("customers/create", "CustomersCreate")]
    public async Task<IActionResult> CreatePost(CustomerEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var customer = new Customer
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = now,
            LastActivityUtc = now,
        };

        Validate(model, customer);

        if (!ModelState.IsValid)
        {
            model.IsNew = true;
            model.CanManage = true;

            return View("Edit", await BuildEditorAsync(model));
        }

        Apply(model, customer, now);

        await _store.CreateAsync(customer, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The customer {0} was added.", customer.GetName()]);

        return RedirectToAction(nameof(Edit), new { customerId = customer.ItemId });
    }

    [Admin("customers/{customerId}", "CustomersEdit")]
    public async Task<IActionResult> Edit(string customerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ViewCustomers))
        {
            return Forbid();
        }

        var customer = string.IsNullOrEmpty(customerId) ? null : await _store.FindByIdAsync(customerId, HttpContext.RequestAborted);

        if (customer is null)
        {
            return NotFound();
        }

        var model = ToViewModel(customer);
        model.CanManage = await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers);

        return View(await BuildEditorAsync(model));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("customers/{customerId}", "CustomersEdit")]
    public async Task<IActionResult> EditPost(string customerId, CustomerEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = string.IsNullOrEmpty(customerId) ? null : await _store.FindByIdAsync(customerId, HttpContext.RequestAborted);

        if (customer is null)
        {
            return NotFound();
        }

        // An anonymized customer keeps nothing personal to edit.
        if (customer.Status == CustomerStatus.Anonymized)
        {
            return RedirectToAction(nameof(Edit), new { customerId });
        }

        Validate(model, customer);

        if (!ModelState.IsValid)
        {
            var invalid = ToViewModel(customer);
            CopyInput(model, invalid);
            invalid.CanManage = true;

            return View(await BuildEditorAsync(invalid));
        }

        Apply(model, customer, _clock.UtcNow);

        try
        {
            await _store.UpdateAsync(customer, HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["Someone else changed this customer while you were editing. Your changes were not saved; review theirs and try again."]);

            return RedirectToAction(nameof(Edit), new { customerId });
        }

        await _notifier.SuccessAsync(H["The customer {0} was saved.", customer.GetName()]);

        return RedirectToAction(nameof(Edit), new { customerId });
    }

    private void Validate(CustomerEditViewModel model, Customer customer)
    {
        // A customer with an account takes the account's email, so it is not edited here.
        var email = string.IsNullOrEmpty(customer.UserId) ? model.Email?.Trim() : customer.Email;

        if (string.IsNullOrEmpty(customer.UserId) && !string.IsNullOrEmpty(email) && !_emailAddress.IsValid(email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter a valid email address."]);
        }

        if (model.Kind == CustomerKind.Business && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            ModelState.AddModelError(nameof(model.CompanyName), S["A business needs a company name."]);
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName) &&
            string.IsNullOrWhiteSpace(model.FirstName) &&
            string.IsNullOrWhiteSpace(model.LastName) &&
            string.IsNullOrWhiteSpace(model.CompanyName) &&
            string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, S["Enter a name, a company or an email, so the customer can be found again."]);
        }

        if (model.Status == CustomerStatus.Anonymized)
        {
            ModelState.AddModelError(nameof(model.Status), S["A customer is anonymized by erasing them, not by changing their status."]);
        }
    }

    private static void Apply(CustomerEditViewModel model, Customer customer, DateTime now)
    {
        customer.Kind = model.Kind;
        customer.Status = model.Status;
        customer.DisplayName = Trim(model.DisplayName);
        customer.FirstName = Trim(model.FirstName);
        customer.LastName = Trim(model.LastName);
        customer.CompanyName = Trim(model.CompanyName);
        customer.TaxId = Trim(model.TaxId);
        customer.Phone = Trim(model.Phone);
        customer.AcceptsMarketing = model.AcceptsMarketing;

        if (string.IsNullOrEmpty(customer.UserId))
        {
            customer.Email = Trim(model.Email);
        }

        customer.ModifiedUtc = now;
    }

    private static CustomerEditViewModel ToViewModel(Customer customer)
        => new()
        {
            CustomerId = customer.ItemId,
            UserId = customer.UserId,
            CreatedUtc = customer.CreatedUtc,
            LastActivityUtc = customer.LastActivityUtc,
            Kind = customer.Kind,
            Status = customer.Status,
            DisplayName = customer.DisplayName,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            CompanyName = customer.CompanyName,
            TaxId = customer.TaxId,
            Email = customer.Email,
            Phone = customer.Phone,
            AcceptsMarketing = customer.AcceptsMarketing,
        };

    private static void CopyInput(CustomerEditViewModel from, CustomerEditViewModel to)
    {
        to.Kind = from.Kind;
        to.Status = from.Status;
        to.DisplayName = from.DisplayName;
        to.FirstName = from.FirstName;
        to.LastName = from.LastName;
        to.CompanyName = from.CompanyName;
        to.TaxId = from.TaxId;
        to.Phone = from.Phone;
        to.AcceptsMarketing = from.AcceptsMarketing;

        if (string.IsNullOrEmpty(to.UserId))
        {
            to.Email = from.Email;
        }
    }

    private async Task<CustomerEditViewModel> BuildEditorAsync(CustomerEditViewModel model)
    {
        model.Kinds =
        [
            new SelectListItem(S["Person"], nameof(CustomerKind.Person)),
            new SelectListItem(S["Business"], nameof(CustomerKind.Business)),
        ];

        model.Statuses =
        [
            new SelectListItem(S["Active"], nameof(CustomerStatus.Active)),
            new SelectListItem(S["Disabled"], nameof(CustomerStatus.Disabled)),
        ];

        if (!string.IsNullOrEmpty(model.UserId) && await _userService.GetUserByUniqueIdAsync(model.UserId) is IUser user)
        {
            model.UserName = user.UserName;
        }

        return model;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RouteValueDictionary FilterRouteValues(CustomersIndexOptions options, PagerParameters pagerParameters)
    {
        var values = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options?.Search))
        {
            values.Add(OptionsSearch, options.Search);
        }

        if (options is not null && options.Filter != CustomerListFilter.Active)
        {
            values.Add(OptionsFilter, options.Filter);
        }

        if (pagerParameters?.PageSize is int pageSize)
        {
            values.Add("pageSize", pageSize);
        }

        return values;
    }
}
