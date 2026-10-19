using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// Gives a customer store credit, or takes it away, from their admin page.
/// </summary>
[Feature(CustomersConstants.Features.StoreCredit)]
public sealed class StoreCreditAdminController : Controller
{
    private readonly ICustomerStore _customerStore;
    private readonly IStoreCreditStore _store;
    private readonly StoreCreditService _storeCredit;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public StoreCreditAdminController(
        ICustomerStore customerStore,
        IStoreCreditStore store,
        StoreCreditService storeCredit,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<StoreCreditAdminController> htmlLocalizer)
    {
        _customerStore = customerStore;
        _store = store;
        _storeCredit = storeCredit;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("customers/{customerId}/store-credit", "CustomersStoreCredit")]
    public async Task<IActionResult> Change(string customerId, decimal? amount, string currency, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageCustomers))
        {
            return Forbid();
        }

        var customer = string.IsNullOrEmpty(customerId) ? null : await _customerStore.FindByIdAsync(customerId, HttpContext.RequestAborted);

        if (customer is null)
        {
            return NotFound();
        }

        currency = currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(customer.UserId))
        {
            await _notifier.ErrorAsync(H["Only customers with an account can be given store credit."]);
        }
        else if (amount is null or 0m)
        {
            await _notifier.ErrorAsync(H["Enter an amount to give, or a negative amount to take away."]);
        }
        else if (currency?.Length != 3)
        {
            await _notifier.ErrorAsync(H["Enter the three-letter currency code, such as USD."]);
        }
        else if (string.IsNullOrWhiteSpace(reason))
        {
            await _notifier.ErrorAsync(H["Say why the credit changes. It's kept with its activity."]);
        }
        else if (await _storeCredit.ChangeAsync(customer.ItemId, currency, amount.Value, reason.Trim(), User.Identity?.Name, cancellationToken: HttpContext.RequestAborted) is { } account)
        {
            await _notifier.SuccessAsync(H["The customer's store credit is now {0}.", ProductPriceFormatter.Format(account.Balance, account.Currency)]);
        }
        else
        {
            await _notifier.ErrorAsync(amount < 0m && await _store.FindAsync(customer.ItemId, currency, HttpContext.RequestAborted) is null
                ? H["The customer has no store credit in {0} to take away.", currency]
                : H["The customer's store credit is being used at a checkout right now. Try again in a moment."]);
        }

        return RedirectToAction("Edit", "CustomersAdmin", new { customerId });
    }
}
