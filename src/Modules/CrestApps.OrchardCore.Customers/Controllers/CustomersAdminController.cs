using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
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

    private static readonly JsonSerializerOptions _exportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ICustomerStore _store;
    private readonly ICustomerPrivacyService _privacy;
    private readonly CustomerAddressBook _addressBook;
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
        ICustomerPrivacyService privacy,
        CustomerAddressBook addressBook,
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
        _privacy = privacy;
        _addressBook = addressBook;
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

        var viewModel = new CustomersIndexViewModel
        {
            Options = options,
            Customers = customers,
            TotalCount = count,
            CanManage = await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers),
            Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
        };

        // The admin-list tag builds a row per customer, in the layout (List or Grid) the site or the user picked.
        viewModel.List = new AdminListContext<Customer>
        {
            Source = customers,
            Pager = viewModel.Pager,
        };

        // The filters render at the end of the list's toolbar.
        viewModel.List.ToolbarActions = await _shapeFactory.CreateAsync("CustomersAdminListFilters", Arguments.From(new { Options = options }));

        return View(viewModel);
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
        model.Addresses = await _addressBook.SummarizeAsync(customer);
        model.Sections = await HttpContext.RequestServices.GetRequiredService<IDisplayManager<Customer>>()
            .BuildDisplayAsync(customer, HttpContext.RequestServices.GetRequiredService<IUpdateModelAccessor>().ModelUpdater, "DetailAdmin");

        await AddHistoryAsync(model, customer);

        return View(await BuildEditorAsync(model));
    }

    // Recent orders, and the merges that joined guests to this customer or this guest to an account.
    private async Task AddHistoryAsync(CustomerEditViewModel model, Customer customer)
    {
        if (HttpContext.RequestServices.GetService<IOrderStore>() is { } orders)
        {
            // An account holder's orders include those from before the customer record existed.
            var (count, recent) = await orders.PageAsync(new OrderQuery
            {
                UserId = customer.UserId,
                CustomerId = string.IsNullOrEmpty(customer.UserId) ? customer.ItemId : null,
                Statuses = [OrderStatus.Paid, OrderStatus.AwaitingPayment],
            }, 0, 20, HttpContext.RequestAborted);

            model.HasOrders = true;
            model.Orders = recent
                .Select(order => new CustomerOrderSummary
                {
                    OrderId = order.ItemId,
                    OrderNumber = order.OrderNumber,
                    CreatedUtc = order.CreatedUtc,
                    Status = order.Status == OrderStatus.Paid ? S["Paid"].Value : S["Awaiting payment"].Value,
                    Total = ProductPriceFormatter.Format(order.Total ?? order.Subtotal, order.Currency),
                })
                .ToArray();
        }

        if (!string.IsNullOrEmpty(customer.MergedIntoCustomerId) && await _store.FindByIdAsync(customer.MergedIntoCustomerId, HttpContext.RequestAborted) is { } into)
        {
            model.MergedInto = new CustomerMergeSummary { CustomerId = into.ItemId, Name = into.GetName() };
            model.MergedUtc = customer.MergedUtc;
        }

        var merged = new List<CustomerMergeSummary>();

        foreach (var mergedId in customer.MergedCustomerIds ?? [])
        {
            if (await _store.FindByIdAsync(mergedId, HttpContext.RequestAborted) is { } guest)
            {
                merged.Add(new CustomerMergeSummary { CustomerId = guest.ItemId, Name = guest.GetName() });
            }
        }

        model.MergedCustomers = merged;
        model.PrivacyLog = customer.PrivacyLog ?? [];
    }

    /// <summary>
    /// Erases the customer's personal data, and the personal details of their orders, for an erasure request.
    /// </summary>
    [HttpPost]
    [Admin("customers/{customerId}/erase", "CustomersErase")]
    public async Task<IActionResult> Erase(string customerId)
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

        if (customer.Status != CustomerStatus.Anonymized)
        {
            await _privacy.EraseAsync(customer, User.Identity?.Name, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The customer's personal data was erased. Their orders stay, without it."]);
        }

        return RedirectToAction(nameof(Edit), new { customerId });
    }

    /// <summary>
    /// Downloads everything the store holds on the customer, for an access request.
    /// </summary>
    [Admin("customers/{customerId}/export", "CustomersExport")]
    public async Task<IActionResult> Export(string customerId)
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

        var export = await _privacy.ExportAsync(customer, User.Identity?.Name, HttpContext.RequestAborted);

        return File(Encoding.UTF8.GetBytes(export.ToJsonString(_exportOptions)), "application/json", $"customer-{customer.ItemId}.json");
    }

    [Admin("customers/{customerId}/addresses/add", "CustomersAddressCreate")]
    public async Task<IActionResult> AddressCreate(string customerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = await FindEditableAsync(customerId);

        return customer is null ? NotFound() : View("AddressEdit", await _addressBook.BuildEditorAsync(customer, null));
    }

    [HttpPost]
    [ActionName(nameof(AddressCreate))]
    [Admin("customers/{customerId}/addresses/add", "CustomersAddressCreate")]
    public Task<IActionResult> AddressCreatePost(string customerId, CustomerAddressViewModel model)
        => SaveAddressAsync(customerId, addressId: null, model);

    [Admin("customers/{customerId}/addresses/{addressId}", "CustomersAddressEdit")]
    public async Task<IActionResult> AddressEdit(string customerId, string addressId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = await FindEditableAsync(customerId);
        var address = customer?.FindAddress(addressId);

        return address is null ? NotFound() : View(await _addressBook.BuildEditorAsync(customer, address));
    }

    [HttpPost]
    [ActionName(nameof(AddressEdit))]
    [Admin("customers/{customerId}/addresses/{addressId}", "CustomersAddressEdit")]
    public Task<IActionResult> AddressEditPost(string customerId, string addressId, CustomerAddressViewModel model)
        => SaveAddressAsync(customerId, addressId, model);

    [HttpPost]
    [Admin("customers/{customerId}/addresses/{addressId}/delete", "CustomersAddressDelete")]
    public async Task<IActionResult> AddressDelete(string customerId, string addressId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = await FindEditableAsync(customerId);

        if (customer is null || !customer.RemoveAddress(addressId))
        {
            return NotFound();
        }

        if (await TrySaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was deleted."]);
        }

        return RedirectToAction(nameof(Edit), new { customerId });
    }

    private async Task<IActionResult> SaveAddressAsync(string customerId, string addressId, CustomerAddressViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = await FindEditableAsync(customerId);
        var address = customer?.FindAddress(addressId);

        if (customer is null || (addressId is not null && address is null))
        {
            return NotFound();
        }

        var value = await _addressBook.ValidateAsync(customer, model, address is null, ModelState);

        if (value is null)
        {
            return View("AddressEdit", await _addressBook.BuildEditorAsync(customer, address, model));
        }

        CustomerAddressBook.Apply(customer, address, model, value);

        if (await TrySaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was saved."]);
        }

        return RedirectToAction(nameof(Edit), new { customerId });
    }

    // An anonymized customer keeps nothing personal, so they have no address book to change.
    private async Task<Customer> FindEditableAsync(string customerId)
    {
        var customer = string.IsNullOrEmpty(customerId) ? null : await _store.FindByIdAsync(customerId, HttpContext.RequestAborted);

        return customer?.Status is CustomerStatus.Anonymized or CustomerStatus.Merged ? null : customer;
    }

    private async Task<bool> TrySaveAsync(Customer customer)
    {
        customer.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _store.UpdateAsync(customer, HttpContext.RequestAborted);

            return true;
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["Someone else changed this customer at the same time. Your change was not saved; try again."]);

            return false;
        }
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
            invalid.Addresses = await _addressBook.SummarizeAsync(customer);

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
        customer.IsTaxExempt = model.IsTaxExempt;
        customer.TaxExemptionReason = model.IsTaxExempt ? Trim(model.TaxExemptionReason) : null;
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
            IsTaxExempt = customer.IsTaxExempt,
            TaxExemptionReason = customer.TaxExemptionReason,
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
        to.IsTaxExempt = from.IsTaxExempt;
        to.TaxExemptionReason = from.TaxExemptionReason;
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
