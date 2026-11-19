using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout.Models;
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
/// Adds the Business tab to an account's page: its terms, what it owes and its buyers. The account's invoices are in the
/// Invoices tab, from the Invoicing module, and the orders placed for it in the Orders tab, from the Orders module.
/// </summary>
internal sealed class B2BAccountCardsDisplayDriver : ContentDisplayDriver
{
    private readonly AccountTermsService _terms;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public B2BAccountCardsDisplayDriver(
        AccountTermsService terms,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
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

        var account = new B2BAccount(contentItem);

        return Combine(
            Initialize<B2BAccountCardsViewModel>("B2BAccountCards", async model =>
            {
                var (owed, currency) = await _terms.GetOwedAsync(account);

                model.Account = account;
                model.Owed = owed;
                model.Currency = currency ?? (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Business)));
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
}
