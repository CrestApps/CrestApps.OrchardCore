using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.ViewModels;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Banking.Controllers;

/// <summary>
/// Banking › Bank accounts: every bank account, its numbers, verification and status.
/// </summary>
[Admin]
public sealed class BankAccountsController : CatalogAdminController<BankAccount>
{
    private readonly IBankAccountService _bankAccountService;
    private readonly BankAccountLinkFlow _linkFlow;

    public BankAccountsController(
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        BankAccountLinkFlow linkFlow,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BankAccount> displayManager,
        INotifier notifier,
        IHtmlLocalizer<BankAccountsController> htmlLocalizer,
        IStringLocalizer<BankAccountsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _bankAccountService = bankAccountService;
        _linkFlow = linkFlow;
    }

    protected override Permission ManagePermission
        => BankingPermissions.ManageBankAccounts;

    protected override LocalizedString CreateDisplayName
        => S["Bank account"];

    protected override LocalizedString NewDisplayName
        => S["New bank account"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The bank account has been added. It can receive payments once it is verified."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The bank account has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The bank account has been removed."];

    [Admin("banking/accounts", "BankAccountsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("banking/accounts", "BankAccountsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("banking/accounts/create", "BankAccountsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("banking/accounts/create", "BankAccountsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("banking/accounts/edit/{id}", "BankAccountsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("banking/accounts/edit/{id}", "BankAccountsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("banking/accounts/delete/{id}", "BankAccountsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);

    /// <inheritdoc />
    protected override async Task<bool> CanDeleteAsync(BankAccount model)
    {
        var usages = await _bankAccountService.GetUsagesAsync(model, HttpContext.RequestAborted);

        if (usages.Count == 0)
        {
            return true;
        }

        await Notifier.ErrorAsync(H["{0} is in use and can't be deleted: {1} Close it instead, from its edit page.", model.Name, string.Join(" ", usages)]);

        return false;
    }

    /// <summary>
    /// Shows the full account number once, to a user who may reveal it, and writes the reveal to the Audit Trail.
    /// </summary>
    /// <param name="id">The account's id.</param>
    [HttpPost]
    [Admin("banking/accounts/reveal/{id}", "BankAccountsReveal")]
    public async Task<IActionResult> Reveal(string id)
    {
        if (!await AuthorizeAsync() || !await AuthorizationService.AuthorizeAsync(User, BankingPermissions.RevealBankAccountNumbers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await Manager.FindByIdAsync(id) is not { } account)
        {
            return NotFound();
        }

        var number = await _bankAccountService.RevealAccountNumberAsync(account, S["Shown on the bank account page"]);

        if (number is null)
        {
            await Notifier.ErrorAsync(H["The account number can't be read on this site, for example after the database was copied from another one. Enter it again."]);

            return RedirectToAction(nameof(Edit), new { id });
        }

        // The page holds a full account number: browsers and proxies must not keep it.
        Response.Headers.CacheControl = "no-store";

        return View(new BankAccountRevealViewModel
        {
            ItemId = account.ItemId,
            Name = account.Name,
            RoutingNumber = account.RoutingNumber,
            AccountNumber = number,
        });
    }

    /// <summary>
    /// Starts checking an account with a verifier.
    /// </summary>
    /// <param name="id">The account's id.</param>
    /// <param name="verifier">The verifier's name.</param>
    [HttpPost]
    [Admin("banking/accounts/verify/{id}", "BankAccountsVerify")]
    public async Task<IActionResult> Verify(string id, string verifier)
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await Manager.FindByIdAsync(id) is not { } account)
        {
            return NotFound();
        }

        var result = await _bankAccountService.StartVerificationAsync(account, verifier);

        if (result.Succeeded)
        {
            await Notifier.SuccessAsync(H["Checking the account has started."]);
        }
        else
        {
            await Notifier.ErrorAsync(H["The account can't be checked: {0}", result.Message]);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Shows the page where the account holder links their bank with a provider, which replaces the account's numbers
    /// with the bank's.
    /// </summary>
    /// <param name="id">The account's id.</param>
    /// <param name="provider">The link provider's name.</param>
    [Admin("banking/accounts/link/{id}/{provider}", "BankAccountsLink")]
    public async Task<IActionResult> Link(string id, string provider)
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        if (await FindLinkTargetAsync(id, provider) is not { } target)
        {
            return NotFound();
        }

        var (model, error) = await _linkFlow.StartAsync(target.Provider, target.Context);

        if (model is null)
        {
            await Notifier.ErrorAsync(H["The bank can't be linked: {0}", error]);

            return RedirectToAction(nameof(Edit), new { id });
        }

        model.FormAction = Url.Action(nameof(Link), new { id, provider = target.Provider.Name });
        model.CancelUrl = Url.Action(nameof(Edit), new { id });

        return View(model);
    }

    /// <summary>
    /// Saves the account the holder linked.
    /// </summary>
    /// <param name="id">The account's id.</param>
    /// <param name="provider">The link provider's name.</param>
    /// <param name="clientResult">What the provider's browser part posted.</param>
    [HttpPost]
    [ActionName(nameof(Link))]
    [Admin("banking/accounts/link/{id}/{provider}", "BankAccountsLink")]
    public async Task<IActionResult> LinkPost(string id, string provider, string clientResult)
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        if (await FindLinkTargetAsync(id, provider) is not { } target)
        {
            return NotFound();
        }

        var errors = await _linkFlow.CompleteAsync(target.Provider, target.Context, clientResult, name: null, holderName: null);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                await Notifier.ErrorAsync(H["The bank was not linked: {0}", error]);
            }

            return RedirectToAction(nameof(Link), new { id, provider = target.Provider.Name });
        }

        await Notifier.SuccessAsync(H["The bank account has been linked with {0}.", target.Provider.DisplayName]);

        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<(IBankAccountLinkProvider Provider, BankAccountLinkContext Context)?> FindLinkTargetAsync(string id, string provider)
    {
        if (string.IsNullOrEmpty(id)
            || await _linkFlow.FindProviderAsync(provider) is not { } linkProvider
            || await Manager.FindByIdAsync(id) is not { } account)
        {
            return null;
        }

        return (linkProvider, new BankAccountLinkContext
        {
            Account = account,
            IsNew = false,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        });
    }
}
