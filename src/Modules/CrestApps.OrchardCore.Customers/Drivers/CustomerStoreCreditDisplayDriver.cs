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
/// Adds the customer's store credit, its activity and a way to change it to their admin page.
/// </summary>
internal sealed class CustomerStoreCreditDisplayDriver : DisplayDriver<Customer>
{
    private readonly IStoreCreditStore _store;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerStoreCreditDisplayDriver(
        IStoreCreditStore store,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
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

        return Initialize<CustomerStoreCreditViewModel>("CustomerStoreCredit_DetailAdmin", async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.CustomerId = customer.ItemId;
            model.Accounts = await _store.ListByCustomerAsync(customer.ItemId);
            model.HasAccount = !string.IsNullOrEmpty(customer.UserId);
            model.CanManage = user is not null && await _authorizationService.AuthorizeAsync(user, CustomerPermissions.ManageCustomers);
            model.DefaultCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        }).Location("Content:5");
    }
}
