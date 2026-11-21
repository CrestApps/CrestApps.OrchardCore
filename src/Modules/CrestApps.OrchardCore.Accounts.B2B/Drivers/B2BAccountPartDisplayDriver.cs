using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// The B2B settings on an account's editor: whether it buys as a business, its terms, budget, price list, each
/// buyer's order limit and punchout. The buyers themselves are the account's members, and the places it ships to its
/// locations, each edited with its own part. Only users who manage B2B accounts see these settings or change them.
/// </summary>
internal sealed class B2BAccountPartDisplayDriver : ContentPartDisplayDriver<B2BAccountPart>
{
    private readonly IPriceListStore _priceLists;
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public B2BAccountPartDisplayDriver(
        IPriceListStore priceLists,
        UserManager<IUser> userManager,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<B2BAccountPartDisplayDriver> htmlLocalizer,
        IStringLocalizer<B2BAccountPartDisplayDriver> stringLocalizer)
    {
        _priceLists = priceLists;
        _userManager = userManager;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(B2BAccountPart part, BuildPartEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var model = new B2BAccountPartViewModel
        {
            Enabled = part.Enabled,
            TaxId = part.TaxId,
            BillingEmail = part.BillingEmail,
            NetDays = part.Terms?.NetDays,
            DepositPercent = part.Terms?.DepositPercent ?? 0m,
            CreditLimit = part.Terms?.CreditLimit,
            RequirePurchaseOrder = part.Terms?.RequirePurchaseOrder == true,
            MonthlyBudget = part.MonthlyBudget,
            PriceListId = part.PriceListId,
            PunchoutEnabled = part.Punchout?.Enabled == true,
            PunchoutUserId = part.Punchout?.UserId,
            PunchoutAllowLinkSignIn = part.Punchout?.AllowLinkSignIn == true,
            PunchoutKeyCreatedUtc = part.Punchout?.KeyCreatedUtc,
        };

        return await BuildEditorAsync(model, part, context);
    }

    public override async Task<IDisplayResult> UpdateAsync(B2BAccountPart part, UpdatePartEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var model = new B2BAccountPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!await ApplyAsync(model, part, context.Updater.ModelState))
        {
            model.PunchoutKeyCreatedUtc = part.Punchout?.KeyCreatedUtc;

            return await BuildEditorAsync(model, part, context);
        }

        if (_newPunchoutKey is not null)
        {
            await _notifier.WarningAsync(H["The new punchout key is <code>{0}</code>. Copy it into the procurement system now: it isn't shown again. Its punchout address is <code>{1}</code>.", _newPunchoutKey, PunchoutUrl(part.ContentItem.ContentItemId)]);
        }

        return await EditAsync(part, context);
    }

    private string _newPunchoutKey;

    private async Task<IDisplayResult> BuildEditorAsync(B2BAccountPartViewModel source, B2BAccountPart part, BuildPartEditorContext context)
    {
        await PopulateAsync(source, part);

        return Initialize<B2BAccountPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.AccountId = source.AccountId;
            model.Enabled = source.Enabled;
            model.TaxId = source.TaxId;
            model.BillingEmail = source.BillingEmail;
            model.NetDays = source.NetDays;
            model.DepositPercent = source.DepositPercent;
            model.CreditLimit = source.CreditLimit;
            model.RequirePurchaseOrder = source.RequirePurchaseOrder;
            model.MonthlyBudget = source.MonthlyBudget;
            model.PriceListId = source.PriceListId;
            model.OrderLimits = source.OrderLimits;
            model.StoreCurrency = source.StoreCurrency;
            model.PriceLists = source.PriceLists;
            model.Members = source.Members;
            model.PunchoutEnabled = source.PunchoutEnabled;
            model.PunchoutUserId = source.PunchoutUserId;
            model.PunchoutAllowLinkSignIn = source.PunchoutAllowLinkSignIn;
            model.PunchoutKeyCreatedUtc = source.PunchoutKeyCreatedUtc;
            model.PunchoutUrl = source.PunchoutUrl;
            model.Prefix = Prefix;
        }).Location(AccountPageTabs.EditorLocation(AccountPageTabs.Business));
    }

    // Checks the form and copies it onto the part. The order limits and the punchout buyer are kept by user id; a
    // member who leaves the account loses both.
    private async Task<bool> ApplyAsync(B2BAccountPartViewModel model, B2BAccountPart part, ModelStateDictionary modelState)
    {
        if (model.NetDays is < 0 or > 365)
        {
            modelState.AddModelError(Key(nameof(model.NetDays)), S["Enter the days an invoice is due in, from 0 to 365, or leave it blank for no terms."]);
        }

        if (model.DepositPercent is < 0m or > 100m)
        {
            modelState.AddModelError(Key(nameof(model.DepositPercent)), S["Enter a deposit from 0 to 100 percent."]);
        }

        if (model.CreditLimit is < 0m || model.MonthlyBudget is < 0m)
        {
            modelState.AddModelError(Key(nameof(model.CreditLimit)), S["A credit limit or budget can't be negative."]);
        }

        var members = Members(part.ContentItem);
        var limits = new List<BuyerOrderLimit>();

        foreach (var row in model.OrderLimits ?? [])
        {
            if (string.IsNullOrEmpty(row?.UserId) || row.Limit is not > 0m || limits.Any(limit => limit.UserId == row.UserId))
            {
                continue;
            }

            limits.Add(new BuyerOrderLimit { UserId = row.UserId, Limit = row.Limit.Value });
        }

        // The procurement system shops as one of the account's members, and never as a site administrator.
        string punchoutUserId = null;

        if (model.PunchoutEnabled)
        {
            punchoutUserId = string.IsNullOrWhiteSpace(model.PunchoutUserId) ? null : model.PunchoutUserId;
            var punchoutUser = punchoutUserId is null ? null : await _userManager.FindByIdAsync(punchoutUserId);

            if (punchoutUser is null || members.FindMember(punchoutUserId) is null)
            {
                modelState.AddModelError(Key(nameof(model.PunchoutUserId)), S["Choose one of the account's members for punchout."]);
            }
            else if (await _userManager.IsInRoleAsync(punchoutUser, OrchardCoreConstants.Roles.Administrator))
            {
                modelState.AddModelError(Key(nameof(model.PunchoutUserId)), S["A site administrator can't be the punchout buyer. Use a user who only buys."]);
            }
        }

        if (!modelState.IsValid)
        {
            return false;
        }

        part.Enabled = model.Enabled;
        part.TaxId = string.IsNullOrWhiteSpace(model.TaxId) ? null : model.TaxId.Trim();
        part.BillingEmail = string.IsNullOrWhiteSpace(model.BillingEmail) ? null : model.BillingEmail.Trim();
        part.Terms = new AccountTerms
        {
            NetDays = model.NetDays,
            DepositPercent = model.DepositPercent,
            CreditLimit = model.CreditLimit,
            RequirePurchaseOrder = model.RequirePurchaseOrder,
        };
        part.MonthlyBudget = model.MonthlyBudget;
        part.PriceListId = string.IsNullOrWhiteSpace(model.PriceListId) ? null : model.PriceListId;
        part.OrderLimits = limits;
        part.Punchout ??= new AccountPunchout();
        part.Punchout.Enabled = model.PunchoutEnabled;
        part.Punchout.UserId = punchoutUserId;
        part.Punchout.AllowLinkSignIn = model.PunchoutEnabled && model.PunchoutAllowLinkSignIn;

        // A new key, asked for or the first; it's shown once.
        _newPunchoutKey = null;

        if (model.PunchoutEnabled && (model.NewPunchoutKey || string.IsNullOrEmpty(part.Punchout.KeyHash)))
        {
            var (key, hash) = PunchoutService.NewKey();

            _newPunchoutKey = key;
            part.Punchout.KeyHash = hash;
            part.Punchout.KeyCreatedUtc = _clock.UtcNow;
        }

        return true;
    }

    private async Task PopulateAsync(B2BAccountPartViewModel model, B2BAccountPart part)
    {
        var accountId = part.ContentItem.ContentItemId;
        var members = Members(part.ContentItem).Members ?? [];
        var posted = (model.OrderLimits ?? [])
            .Where(row => !string.IsNullOrEmpty(row?.UserId))
            .GroupBy(row => row.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Limit, StringComparer.Ordinal);

        // One row for each of the account's current members: the limit as posted, or as saved.
        model.OrderLimits = [.. members
            .Where(member => !string.IsNullOrEmpty(member?.UserId))
            .Select(member => new BuyerOrderLimitRowViewModel
            {
                UserId = member.UserId,
                User = member.Email ?? member.UserName,
                Role = member.Role,
                Limit = posted.TryGetValue(member.UserId, out var limit)
                    ? limit
                    : part.OrderLimits?.FirstOrDefault(saved => saved?.UserId == member.UserId)?.Limit,
            })];

        model.Members = [.. members
            .Where(member => !string.IsNullOrEmpty(member?.UserId))
            .Select(member => new SelectListItem(member.Email ?? member.UserName, member.UserId, member.UserId == model.PunchoutUserId))];

        model.AccountId = accountId;
        model.PunchoutUrl = string.IsNullOrEmpty(accountId) ? null : PunchoutUrl(accountId);
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        model.PriceLists = [.. (await _priceLists.ListAllAsync()).Select(list => new SelectListItem(list.Name, list.ItemId, list.ItemId == model.PriceListId))];
    }

    private async Task<bool> CanManageAsync()
        => _httpContextAccessor.HttpContext?.User is { } user &&
        await _authorizationService.AuthorizeAsync(user, B2BAccountsPermissions.ManageB2BAccounts);

    private string PunchoutUrl(string accountId)
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}/punchout/oci/{accountId}";
    }

    private string Key(string name)
        => $"{Prefix}.{name}";

    private static AccountMembersPart Members(ContentItem contentItem)
        => contentItem.TryGet<AccountMembersPart>(out var part) ? part : new AccountMembersPart();
}
