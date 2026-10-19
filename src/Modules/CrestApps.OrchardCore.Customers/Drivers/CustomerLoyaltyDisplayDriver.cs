using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Adds the customer's loyalty points, their activity and a way to change them to their admin page.
/// </summary>
internal sealed class CustomerLoyaltyDisplayDriver : DisplayDriver<Customer>
{
    private readonly ILoyaltyStore _store;
    private readonly LoyaltyService _loyalty;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerLoyaltyDisplayDriver(
        ILoyaltyStore store,
        LoyaltyService loyalty,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _loyalty = loyalty;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(Customer customer, BuildDisplayContext context)
    {
        if (context.DisplayType != "DetailAdmin")
        {
            return null;
        }

        return Initialize<CustomerLoyaltyViewModel>("CustomerLoyalty_DetailAdmin", async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.CustomerId = customer.ItemId;
            model.Account = await _store.FindByCustomerAsync(customer.ItemId);
            model.Settings = await _loyalty.GetSettingsAsync();
            model.Currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
            model.HasAccount = !string.IsNullOrEmpty(customer.UserId);
            model.CanManage = user is not null && await _authorizationService.AuthorizeAsync(user, CustomerPermissions.ManageCustomers);
        }).Location("Content:6");
    }
}
