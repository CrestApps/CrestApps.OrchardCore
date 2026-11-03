using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// The B2B settings on an account's editor: whether it buys as a business, its buyers, locations, terms, budget, price
/// list and punchout. Only users who manage B2B accounts see them or change them.
/// </summary>
internal sealed class B2BAccountPartDisplayDriver : ContentPartDisplayDriver<B2BAccountPart>
{
    // Empty rows the editor offers to add buyers and locations.
    private const int SpareRows = 2;

    private readonly IB2BAccountStore _accounts;
    private readonly IPriceListStore _priceLists;
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public B2BAccountPartDisplayDriver(
        IB2BAccountStore accounts,
        IPriceListStore priceLists,
        UserManager<IUser> userManager,
        ISiteService siteService,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<B2BAccountPartDisplayDriver> htmlLocalizer,
        IStringLocalizer<B2BAccountPartDisplayDriver> stringLocalizer)
    {
        _accounts = accounts;
        _priceLists = priceLists;
        _userManager = userManager;
        _siteService = siteService;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
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
            PunchoutUser = FindBuyer(part, part.Punchout?.UserId) is { } punchoutBuyer ? punchoutBuyer.Email ?? punchoutBuyer.UserName : null,
            PunchoutKeyCreatedUtc = part.Punchout?.KeyCreatedUtc,
            Buyers = [.. (part.Buyers ?? []).Select(buyer => new AccountBuyerRowViewModel { User = buyer.Email ?? buyer.UserName, Role = buyer.Role, OrderLimit = buyer.OrderLimit })],
        };

        foreach (var location in part.Locations ?? [])
        {
            model.Locations.Add(new AccountLocationRowViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                Address = await _addressEditor.BuildAsync(location.Address, LocationEditorOptions(model)),
            });
        }

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
        await PopulateAsync(source, part.ContentItem.ContentItemId);

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
            model.Buyers = source.Buyers;
            model.Locations = source.Locations;
            model.StoreCurrency = source.StoreCurrency;
            model.PriceLists = source.PriceLists;
            model.PunchoutEnabled = source.PunchoutEnabled;
            model.PunchoutUser = source.PunchoutUser;
            model.PunchoutKeyCreatedUtc = source.PunchoutKeyCreatedUtc;
            model.PunchoutUrl = source.PunchoutUrl;
            model.Prefix = Prefix;
        });
    }

    // Checks the form and copies it onto the part. Buyers are found by their user name or email, and a user buys for
    // one account only.
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

        var accountId = part.ContentItem.ContentItemId;
        var buyers = new List<AccountBuyer>();

        foreach (var row in (model.Buyers ?? []).Where(row => !string.IsNullOrWhiteSpace(row.User)))
        {
            var name = row.User.Trim();
            var user = await _userManager.FindByEmailAsync(name) ?? await _userManager.FindByNameAsync(name);

            if (user is null)
            {
                modelState.AddModelError(Key(nameof(model.Buyers)), S["There's no user {0}. Add the buyer as a user first.", name]);

                continue;
            }

            var userId = await _userManager.GetUserIdAsync(user);

            if (buyers.Any(buyer => buyer.UserId == userId))
            {
                continue;
            }

            if (await _accounts.FindByBuyerAsync(userId) is { } other && other.AccountId != accountId)
            {
                modelState.AddModelError(Key(nameof(model.Buyers)), S["{0} already buys for {1}.", name, other.Name]);

                continue;
            }

            var existing = FindBuyer(part, userId);

            buyers.Add(new AccountBuyer
            {
                UserId = userId,
                UserName = await _userManager.GetUserNameAsync(user),
                Email = await _userManager.GetEmailAsync(user),
                Role = row.Role,
                OrderLimit = row.OrderLimit is > 0m ? row.OrderLimit : null,
                ContactContentItemId = existing?.ContactContentItemId,
            });
        }

        // Each location is checked against its country's rules by the shared address editor; a row left blank is no
        // location. Its recipient is the account.
        var locations = new List<AccountLocation>();
        var locationOptions = LocationEditorOptions(model);
        var accountName = part.ContentItem.DisplayText?.Trim();

        for (var i = 0; i < (model.Locations?.Count ?? 0); i++)
        {
            var row = model.Locations[i];
            row.Address ??= new AddressEditorViewModel();

            if (string.IsNullOrWhiteSpace(row.Name) && !row.Address.HasAnyValue())
            {
                continue;
            }

            row.Address.Name = accountName;
            row.Address.Company = accountName;

            var result = await _addressEditor.ValidateAsync(row.Address, modelState, LocationEditorPrefix(i), locationOptions);

            if (!result.IsValid)
            {
                continue;
            }

            locations.Add(new AccountLocation
            {
                LocationId = string.IsNullOrEmpty(row.LocationId) ? IdGenerator.GenerateId() : row.LocationId,
                Name = string.IsNullOrWhiteSpace(row.Name) ? result.Address.City : row.Name.Trim(),
                Address = result.Address,
            });
        }

        // The procurement system shops as one of the account's buyers, and never as a site administrator.
        string punchoutUserId = null;

        if (model.PunchoutEnabled)
        {
            var punchoutUser = string.IsNullOrWhiteSpace(model.PunchoutUser)
                ? null
                : await _userManager.FindByEmailAsync(model.PunchoutUser.Trim()) ?? await _userManager.FindByNameAsync(model.PunchoutUser.Trim());

            punchoutUserId = punchoutUser is null ? null : await _userManager.GetUserIdAsync(punchoutUser);

            if (punchoutUserId is null || !buyers.Any(buyer => buyer.UserId == punchoutUserId))
            {
                modelState.AddModelError(Key(nameof(model.PunchoutUser)), S["Choose one of the account's buyers for punchout, by user name or email."]);
            }
            else if (await _userManager.IsInRoleAsync(punchoutUser, "Administrator"))
            {
                modelState.AddModelError(Key(nameof(model.PunchoutUser)), S["A site administrator can't be the punchout buyer. Use a user who only buys."]);
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
        part.Buyers = buyers;
        part.Locations = locations;
        part.Punchout ??= new AccountPunchout();
        part.Punchout.Enabled = model.PunchoutEnabled;
        part.Punchout.UserId = punchoutUserId;

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

    private async Task PopulateAsync(B2BAccountPartViewModel model, string accountId)
    {
        model.AccountId = accountId;
        model.PunchoutUrl = string.IsNullOrEmpty(accountId) ? null : PunchoutUrl(accountId);
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        model.PriceLists = [.. (await _priceLists.ListAllAsync()).Select(list => new SelectListItem(list.Name, list.ItemId, list.ItemId == model.PriceListId))];

        for (var i = 0; i < SpareRows; i++)
        {
            model.Buyers.Add(new AccountBuyerRowViewModel());
        }

        model.Locations.Add(new AccountLocationRowViewModel());

        var locationOptions = LocationEditorOptions(model);

        for (var i = 0; i < model.Locations.Count; i++)
        {
            var row = model.Locations[i];
            row.Address ??= new AddressEditorViewModel();

            await _addressEditor.PopulateAsync(row.Address, locationOptions);
            row.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(row.Address, LocationEditorPrefix(i));
        }
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

    private string LocationEditorPrefix(int index)
        => $"{Prefix}.{nameof(B2BAccountPartViewModel.Locations)}[{index}].{nameof(AccountLocationRowViewModel.Address)}";

    private static AccountBuyer FindBuyer(B2BAccountPart part, string userId)
        => string.IsNullOrEmpty(userId) ? null : part.Buyers?.FirstOrDefault(buyer => string.Equals(buyer.UserId, userId, StringComparison.Ordinal));

    // A location is where the account receives orders, addressed to the account itself, so the editor asks for neither
    // a recipient nor a company. A new one starts in the country of the account's first location.
    private static AddressEditorOptions LocationEditorOptions(B2BAccountPartViewModel model)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = model.Locations?.Select(row => row.Address?.Country).FirstOrDefault(country => !string.IsNullOrWhiteSpace(country)),
        };
}
