using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// Gives a customer loyalty points, or takes them away, from their admin page.
/// </summary>
[Feature(CustomersConstants.Features.Loyalty)]
public sealed class LoyaltyAdminController : Controller
{
    private readonly ICustomerStore _customerStore;
    private readonly ILoyaltyStore _store;
    private readonly LoyaltyService _loyalty;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public LoyaltyAdminController(
        ICustomerStore customerStore,
        ILoyaltyStore store,
        LoyaltyService loyalty,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<LoyaltyAdminController> htmlLocalizer)
    {
        _customerStore = customerStore;
        _store = store;
        _loyalty = loyalty;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("customers/{customerId}/loyalty", "CustomersLoyalty")]
    public async Task<IActionResult> Change(string customerId, int? points, string reason)
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

        if (string.IsNullOrEmpty(customer.UserId))
        {
            await _notifier.ErrorAsync(H["Only customers with an account can be given points."]);
        }
        else if (points is null or 0)
        {
            await _notifier.ErrorAsync(H["Enter points to give, or a negative number to take away."]);
        }
        else if (string.IsNullOrWhiteSpace(reason))
        {
            await _notifier.ErrorAsync(H["Say why the points change. It's kept with their activity."]);
        }
        else if (await _loyalty.ChangeAsync(customer.ItemId, points.Value, LoyaltyActivityKind.Adjusted, reason.Trim(), User.Identity?.Name, cancellationToken: HttpContext.RequestAborted) is { } account)
        {
            await _notifier.SuccessAsync(H["The customer now has {0:N0} points.", account.Points]);
        }
        else
        {
            await _notifier.ErrorAsync(points < 0 && await _store.FindByCustomerAsync(customer.ItemId, HttpContext.RequestAborted) is null
                ? H["The customer has no points to take away."]
                : H["The customer's points are being used at a checkout right now. Try again in a moment."]);
        }

        return RedirectToAction("Edit", "CustomersAdmin", new { customerId });
    }
}
