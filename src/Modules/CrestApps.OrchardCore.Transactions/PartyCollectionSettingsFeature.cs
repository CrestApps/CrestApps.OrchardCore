using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// With Accounts on: an account's page shows and changes whether the account is reminded and charged late fees.
/// </summary>
[RequireFeatures(AccountsConstants.Feature)]
public sealed class AccountCollectionSettingsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IContentDisplayDriver, AccountCollectionSettingsDisplayDriver>();
}

/// <summary>
/// With Customers on: a customer's page shows and changes whether the customer is reminded and charged late fees.
/// </summary>
[RequireFeatures(CustomersConstants.Features.Customers)]
public sealed class CustomerCollectionSettingsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDisplayDriver<Customer, CustomerCollectionSettingsDisplayDriver>();
}

/// <summary>
/// Builds the collection settings card of a party's page.
/// </summary>
internal static class PartyCollectionSettingsCard
{
    public const string ShapeType = "PartyCollectionSettings_DetailAdmin";

    public static async Task FillAsync(PartyCollectionSettingsViewModel model, PartyRef party, IServiceProvider services)
    {
        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var settings = await services.GetRequiredService<IPartyCollectionSettingsService>().GetAsync(party);

        model.Party = party.ToString();
        model.RemindersPaused = settings?.RemindersPaused ?? false;
        model.LateFeesExempt = settings?.LateFeesExempt ?? false;
        model.CanManage = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext?.User, TransactionsPermissions.ManageTransactions);
        model.ReturnUrl = httpContext is null ? null : $"{httpContext.Request.PathBase}{httpContext.Request.Path}{httpContext.Request.QueryString}";
    }
}

/// <summary>
/// The collection settings card on an account's page, in its Overview section.
/// </summary>
public sealed class AccountCollectionSettingsDisplayDriver : ContentDisplayDriver
{
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountCollectionSettingsDisplayDriver(IServiceProvider services, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _services = services;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactions))
        {
            return null;
        }

        return Initialize<PartyCollectionSettingsViewModel>(PartyCollectionSettingsCard.ShapeType, async model => await PartyCollectionSettingsCard.FillAsync(model, new PartyRef(PartyKinds.Account, contentItem.ContentItemId), _services))
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Overview));
    }
}

/// <summary>
/// The collection settings card on a customer's page.
/// </summary>
public sealed class CustomerCollectionSettingsDisplayDriver : DisplayDriver<Customer>
{
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerCollectionSettingsDisplayDriver(IServiceProvider services, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _services = services;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(Customer customer, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactions))
        {
            return null;
        }

        return Initialize<PartyCollectionSettingsViewModel>(PartyCollectionSettingsCard.ShapeType, async model => await PartyCollectionSettingsCard.FillAsync(model, new PartyRef(PartyKinds.Customer, customer.ItemId), _services))
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:6");
    }
}

/// <summary>
/// Saves whether a party is reminded and charged late fees.
/// </summary>
[Admin("transactions/party-collection/{action}", "PartyCollectionSettings{action}")]
public sealed class PartyCollectionSettingsController : Controller
{
    private readonly IPartyCollectionSettingsService _settings;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public PartyCollectionSettingsController(IPartyCollectionSettingsService settings, IAuthorizationService authorizationService, INotifier notifier, IHtmlLocalizer<PartyCollectionSettingsController> htmlLocalizer)
    {
        _settings = settings;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost]
    public async Task<IActionResult> Save(string party, bool remindersPaused, bool lateFeesExempt, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TransactionsPermissions.ManageTransactions))
        {
            return Forbid();
        }

        if (!PartyRef.TryParse(party, out var partyRef))
        {
            return BadRequest();
        }

        await _settings.SaveAsync(partyRef, remindersPaused, lateFeesExempt, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The collection settings were saved. The next reminder and late-fee runs use them."]);

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Admin", new { area = "OrchardCore.Admin" });
    }
}
