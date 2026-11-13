using System.Text.Json;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Drivers;

internal sealed class BankAccountDisplayDriver : DisplayDriver<BankAccount>
{
    private readonly IBankAccountService _bankAccountService;
    private readonly IPartyDirectory _partyDirectory;
    private readonly IEnumerable<IUserPartySource> _userPartySources;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public BankAccountDisplayDriver(
        IBankAccountService bankAccountService,
        IPartyDirectory partyDirectory,
        IEnumerable<IUserPartySource> userPartySources,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<BankAccountDisplayDriver> stringLocalizer)
    {
        _bankAccountService = bankAccountService;
        _partyDirectory = partyDirectory;
        _userPartySources = userPartySources;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(BankAccount account, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = account.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("BankAccount_Fields_SummaryAdmin", account)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("BankAccount_Description_SummaryAdmin", account)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Account:5"),
            View("BankAccount_Tags_SummaryAdmin", account)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("BankAccount_Buttons_SummaryAdmin", account).Location("Actions:5"),
            View("BankAccount_ActionsMenu_SummaryAdmin", account).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(BankAccount account, BuildEditorContext context)
    {
        var isOwn = context.GroupId == BankAccountEditorGroups.Own;
        var ownOptions = isOwn ? await GetOwnOwnerOptionsAsync(account.Owner) : [];
        var ownerJson = "[]";

        if (!isOwn && !account.Owner.IsEmpty)
        {
            var name = await _partyDirectory.GetNameAsync(account.Owner) ?? account.Owner.Id;

            ownerJson = JsonSerializer.Serialize(new[] { new { value = account.Owner.ToString(), text = $"{name} ({account.Owner.Kind})" } });
        }

        return Initialize<BankAccountViewModel>("BankAccountFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.IsOwnAccount = isOwn;
            model.Name = account.Name;
            model.Owner = account.Owner.ToString();
            model.OwnerInitialItemsJson = ownerJson;
            model.OwnOwnerOptions = ownOptions;
            model.HolderName = account.HolderName;
            model.Kind = account.Kind;
            model.Country = account.Country;
            model.Currency = account.Currency;
            model.BankName = account.BankName;
            model.RoutingNumber = account.RoutingNumber;
            model.MaskedAccountNumber = BankAccountNumbers.Masked(account);
            model.Status = account.Status;
            model.StatusReason = account.StatusReason;
            model.KindOptions =
            [
                new SelectListItem(S["Checking"], nameof(BankAccountKind.Checking), account.Kind == BankAccountKind.Checking),
                new SelectListItem(S["Savings"], nameof(BankAccountKind.Savings), account.Kind == BankAccountKind.Savings),
            ];
            model.StatusOptions = StatusOptions(account.Status);
        }).Location("Content:1").OnGroup(context.GroupId ?? string.Empty);
    }

    public override async Task<IDisplayResult> UpdateAsync(BankAccount account, UpdateEditorContext context)
    {
        var isOwn = context.GroupId == BankAccountEditorGroups.Own;
        var model = new BankAccountViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        account.Name = Clean(model.Name);
        account.HolderName = Clean(model.HolderName);
        account.Kind = model.Kind;
        account.Country = Clean(model.Country)?.ToUpperInvariant();
        account.Currency = Clean(model.Currency)?.ToUpperInvariant();
        account.BankName = Clean(model.BankName);

        if (isOwn)
        {
            await UpdateOwnOwnerAsync(account, model, context);
        }
        else
        {
            UpdateOwner(account, model, context);
        }

        var numberErrors = _bankAccountService.ApplyNumbers(account, model.RoutingNumber, model.AccountNumber);

        foreach (var error in numberErrors)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + error.Field, error.Message);
        }

        // A refused number already says what to fix.
        if (numberErrors.Count == 0 && !BankAccountNumbers.HasNumbers(account) && (context.IsNew || isOwn))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AccountNumber), S["Enter the routing and account numbers."]);
        }

        if (!isOwn && !context.IsNew && model.Status != account.Status
            && !_bankAccountService.TrySetStatus(account, model.Status, Clean(model.StatusReason)))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Status), S["The account can't move to that status."]);
        }

        return await EditAsync(account, context);
    }

    private void UpdateOwner(BankAccount account, BankAccountViewModel model, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(model.Owner))
        {
            account.Owner = default;

            return;
        }

        if (!PartyRef.TryParse(model.Owner.Trim(), out var owner))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Owner), S["Choose who owns the account."]);

            return;
        }

        account.Owner = owner;
    }

    private async Task UpdateOwnOwnerAsync(BankAccount account, BankAccountViewModel model, UpdateEditorContext context)
    {
        var parties = await GetUserPartiesAsync();

        // A person may only own an account as one of the parties they are.
        if (PartyRef.TryParse(model.Owner?.Trim(), out var owner) && parties.Contains(owner))
        {
            account.Owner = owner;
        }
        else if (account.Owner.IsEmpty || !parties.Contains(account.Owner))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Owner), S["Choose whose account it is."]);
        }
    }

    private async Task<IList<SelectListItem>> GetOwnOwnerOptionsAsync(PartyRef selected)
    {
        var parties = await GetUserPartiesAsync();
        var names = await _partyDirectory.GetNamesAsync(parties);

        return parties
            .Select(party => new SelectListItem(
                party.Kind == PartyKinds.User ? S["Me ({0})", names.GetValueOrDefault(party) ?? party.Id].Value : $"{names.GetValueOrDefault(party) ?? party.Id} ({party.Kind})",
                party.ToString(),
                party == selected))
            .ToList();
    }

    private async Task<IReadOnlyList<PartyRef>> GetUserPartiesAsync()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return await UserParties.GetAsync(_userPartySources, userId);
    }

    private List<SelectListItem> StatusOptions(BankAccountStatus current)
    {
        var allowed = BankAccountStatuses.Transitions.Next(current).Append(current).ToHashSet();

        return new (BankAccountStatus Status, string Text)[]
        {
            (BankAccountStatus.Unverified, S["Unverified"]),
            (BankAccountStatus.VerificationSent, S["Verification sent"]),
            (BankAccountStatus.Verified, S["Verified"]),
            (BankAccountStatus.Failed, S["Failed"]),
            (BankAccountStatus.Closed, S["Closed"]),
        }
            .Where(option => allowed.Contains(option.Status))
            .Select(option => new SelectListItem(option.Text, option.Status.ToString(), option.Status == current))
            .ToList();
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
