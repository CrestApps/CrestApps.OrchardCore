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

    public BankAccountsController(
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BankAccount> displayManager,
        INotifier notifier,
        IHtmlLocalizer<BankAccountsController> htmlLocalizer,
        IStringLocalizer<BankAccountsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _bankAccountService = bankAccountService;
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
}
