using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.ViewModels;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Banking.Controllers;

/// <summary>
/// The signed-in person's own bank accounts: those whose owner is the person or a party they are, such as their staff
/// record. A person can add an account, change it (which takes it back to unverified) and close it, but never sees the
/// full number again, never changes its status and never sees anyone else's.
/// </summary>
[Admin]
public sealed class MyBankAccountsController : Controller
{
    private readonly IBankAccountManager _manager;
    private readonly IBankAccountService _bankAccountService;
    private readonly IEnumerable<IUserPartySource> _userPartySources;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<BankAccount> _displayManager;
    private readonly INotifier _notifier;
    private readonly BankAccountLinkFlow _linkFlow;

    internal readonly IHtmlLocalizer H;

    public MyBankAccountsController(
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        IEnumerable<IUserPartySource> userPartySources,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BankAccount> displayManager,
        INotifier notifier,
        BankAccountLinkFlow linkFlow,
        IHtmlLocalizer<MyBankAccountsController> htmlLocalizer)
    {
        _manager = manager;
        _bankAccountService = bankAccountService;
        _userPartySources = userPartySources;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _linkFlow = linkFlow;
        H = htmlLocalizer;
    }

    [Admin("banking/my-accounts", "MyBankAccountsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts))
        {
            return Forbid();
        }

        var accounts = await _manager.ListByOwnersAsync(await GetPartiesAsync());

        return View(new ListCatalogEntryViewModel<CatalogEntryViewModel<BankAccount>>
        {
            Models = accounts.Select(account => new CatalogEntryViewModel<BankAccount> { Model = account }).ToList(),
            List = new AdminListContext<BankAccount>
            {
                Source = accounts,
            },
        });
    }

    [Admin("banking/my-accounts/create", "MyBankAccountsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts))
        {
            return Forbid();
        }

        var account = await NewAccountAsync();

        return View(new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.BuildEditorAsync(account, _updateModelAccessor.ModelUpdater, isNew: true, groupId: BankAccountEditorGroups.Own),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("banking/my-accounts/create", "MyBankAccountsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts))
        {
            return Forbid();
        }

        var account = await NewAccountAsync();
        var editor = await _displayManager.UpdateEditorAsync(account, _updateModelAccessor.ModelUpdater, isNew: true, groupId: BankAccountEditorGroups.Own);

        if (ModelState.IsValid && await IsValidAsync(account))
        {
            await _manager.CreateAsync(account);
            await _notifier.SuccessAsync(H["Your bank account has been added. It can receive payments once it is verified."]);

            return RedirectToAction(nameof(Index));
        }

        return View(new EditCatalogEntryViewModel { Editor = editor });
    }

    [Admin("banking/my-accounts/edit/{id}", "MyBankAccountsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (await FindOwnAsync(id) is not { } account)
        {
            return await RefuseAsync();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = account.Name,
            Editor = await _displayManager.BuildEditorAsync(account, _updateModelAccessor.ModelUpdater, isNew: false, groupId: BankAccountEditorGroups.Own),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("banking/my-accounts/edit/{id}", "MyBankAccountsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (await FindOwnAsync(id) is not { } account)
        {
            return await RefuseAsync();
        }

        if (account.Status == BankAccountStatus.Closed)
        {
            await _notifier.ErrorAsync(H["A closed account can't be changed. Add a new account instead."]);

            return RedirectToAction(nameof(Index));
        }

        var editor = await _displayManager.UpdateEditorAsync(account, _updateModelAccessor.ModelUpdater, isNew: false, groupId: BankAccountEditorGroups.Own);

        if (ModelState.IsValid && await IsValidAsync(account))
        {
            await _manager.UpdateAsync(account);
            await _notifier.SuccessAsync(H["Your bank account has been updated."]);

            return RedirectToAction(nameof(Index));
        }

        return View(new EditCatalogEntryViewModel { DisplayName = account.Name, Editor = editor });
    }

    [HttpPost]
    [Admin("banking/my-accounts/close/{id}", "MyBankAccountsClose")]
    public async Task<IActionResult> Close(string id)
    {
        if (await FindOwnAsync(id) is not { } account)
        {
            return await RefuseAsync();
        }

        if (await _bankAccountService.ChangeStatusAsync(account, BankAccountStatus.Closed, H["Closed by its owner"].Value))
        {
            await _notifier.SuccessAsync(H["The bank account has been closed. No payment goes to it any more."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the page where the person links their bank with a provider: to add an account when <paramref name="id"/>
    /// is empty, or to replace the numbers of one of their own accounts with the bank's.
    /// </summary>
    /// <param name="provider">The link provider's name.</param>
    /// <param name="id">The account's id, or empty for a new account.</param>
    [Admin("banking/my-accounts/link/{provider}/{id?}", "MyBankAccountsLink")]
    public async Task<IActionResult> Link(string provider, string id)
    {
        if (await FindLinkTargetAsync(provider, id) is not { } target)
        {
            return await RefuseAsync();
        }

        var (model, error) = await _linkFlow.StartAsync(target.Provider, target.Context);

        if (model is null)
        {
            await _notifier.ErrorAsync(H["Your bank can't be linked: {0}", error]);

            return string.IsNullOrEmpty(id) ? RedirectToAction(nameof(Create)) : RedirectToAction(nameof(Edit), new { id });
        }

        model.FormAction = Url.Action(nameof(Link), new { provider = target.Provider.Name, id });
        model.CancelUrl = string.IsNullOrEmpty(id) ? Url.Action(nameof(Create)) : Url.Action(nameof(Edit), new { id });

        return View(model);
    }

    /// <summary>
    /// Saves the account the person linked.
    /// </summary>
    /// <param name="provider">The link provider's name.</param>
    /// <param name="id">The account's id, or empty for a new account.</param>
    /// <param name="clientResult">What the provider's browser part posted.</param>
    /// <param name="model">For a new account, its name and holder.</param>
    [HttpPost]
    [ActionName(nameof(Link))]
    [Admin("banking/my-accounts/link/{provider}/{id?}", "MyBankAccountsLink")]
    public async Task<IActionResult> LinkPost(string provider, string id, string clientResult, BankAccountLinkViewModel model)
    {
        if (await FindLinkTargetAsync(provider, id) is not { } target)
        {
            return await RefuseAsync();
        }

        var errors = await _linkFlow.CompleteAsync(target.Provider, target.Context, clientResult, model?.Name, model?.HolderName);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                await _notifier.ErrorAsync(H["Your bank was not linked: {0}", error]);
            }

            return RedirectToAction(nameof(Link), new { provider = target.Provider.Name, id });
        }

        await _notifier.SuccessAsync(target.Context.Account.CanReceivePayments()
            ? H["Your bank account has been linked and verified. It can receive payments now."]
            : H["Your bank account has been linked. It can receive payments once the bank confirms it."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<(IBankAccountLinkProvider Provider, BankAccountLinkContext Context)?> FindLinkTargetAsync(string provider, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts)
            || await _linkFlow.FindProviderAsync(provider) is not { } linkProvider)
        {
            return null;
        }

        var isNew = string.IsNullOrEmpty(id);
        var account = isNew ? await NewAccountAsync() : await FindOwnAsync(id);

        if (account is null)
        {
            return null;
        }

        return (linkProvider, new BankAccountLinkContext
        {
            Account = account,
            IsNew = isNew,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        });
    }

    private async Task<BankAccount> NewAccountAsync()
    {
        var account = await _manager.NewAsync();
        var parties = await GetPartiesAsync();

        // A person who is also staff is paid as staff, so their staff record owns the account by default.
        account.Owner = parties.FirstOrDefault(party => party.Kind != PartyKinds.User) is { IsEmpty: false } other
            ? other
            : parties.Count > 0 ? parties[0] : default;

        return account;
    }

    private async Task<BankAccount> FindOwnAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || !await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts))
        {
            return null;
        }

        var account = await _manager.FindByIdAsync(id);

        return account is not null && (await GetPartiesAsync()).Contains(account.Owner) ? account : null;
    }

    private async Task<IActionResult> RefuseAsync()
        => await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts) ? NotFound() : Forbid();

    private Task<bool> IsValidAsync(BankAccount account)
        => CatalogEntryValidation.ValidateAsync(_manager, account, _updateModelAccessor.ModelUpdater, nameof(BankAccount));

    private Task<IReadOnlyList<PartyRef>> GetPartiesAsync()
        => UserParties.GetAsync(_userPartySources, User.FindFirstValue(ClaimTypes.NameIdentifier));
}
