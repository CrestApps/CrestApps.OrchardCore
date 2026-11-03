using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// Shows an account's business buying on its page: its terms and what it owes, its buyers, and its latest orders.
/// </summary>
internal sealed class B2BAccountCardsDisplayDriver : ContentDisplayDriver
{
    private const int MaxOrders = 10;

    private readonly IOrderStore _orders;
    private readonly AccountTermsService _terms;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public B2BAccountCardsDisplayDriver(
        IOrderStore orders,
        AccountTermsService terms,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _orders = orders;
        _terms = terms;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<B2BAccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !(contentItem.TryGet<B2BAccountPart>(out var part) && part.Enabled) ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, B2BAccountsPermissions.ManageB2BAccounts))
        {
            return null;
        }

        return Initialize<B2BAccountCardsViewModel>("B2BAccountCards", async model =>
        {
            var account = new B2BAccount(contentItem);
            var (owed, currency) = await _terms.GetOwedAsync(account);
            var (count, orders) = await _orders.PageAsync(new OrderQuery { AccountId = account.AccountId }, 0, MaxOrders);

            model.Account = account;
            model.Owed = owed;
            model.Currency = currency ?? (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
            model.OrderCount = count;
            model.Orders = orders;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:20");
    }
}

/// <summary>
/// An account's business buying, on its page.
/// </summary>
public class B2BAccountCardsViewModel
{
    public B2BAccount Account { get; set; }

    public decimal Owed { get; set; }

    public string Currency { get; set; }

    public int OrderCount { get; set; }

    public IReadOnlyList<Order> Orders { get; set; } = [];
}
