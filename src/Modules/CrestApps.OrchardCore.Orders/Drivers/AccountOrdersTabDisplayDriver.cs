using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Adds the Orders tab to an account's page: the latest orders placed for the account, for users who manage orders.
/// </summary>
internal sealed class AccountOrdersTabDisplayDriver : ContentDisplayDriver
{
    private const int MaxOrders = 25;

    private readonly IOrderStore _orders;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountOrdersTabDisplayDriver(
        IOrderStore orders,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _orders = orders;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        {
            return null;
        }

        return Initialize<AccountOrdersTabViewModel>("AccountOrdersTab", async model =>
        {
            var (count, orders) = await _orders.PageAsync(new OrderQuery { AccountId = contentItem.ContentItemId }, 0, MaxOrders);

            model.Count = count;
            model.Orders = orders;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Orders));
    }
}
