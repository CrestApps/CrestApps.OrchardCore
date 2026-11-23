using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The rows of the check register, and the editor of a check written in the admin. The editor fills an unsaved check,
/// with how it gets its number (and the number of a check written by hand); the controller issues it through the check
/// rules.
/// </summary>
internal sealed class CheckRecordDisplayDriver : DisplayDriver<CheckRecord>
{
    private readonly IBankAccountManager _bankAccounts;
    private readonly ILocalClock _localClock;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;

    public CheckRecordDisplayDriver(
        IBankAccountManager bankAccounts,
        ILocalClock localClock,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IStringLocalizer<CheckRecordDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _bankAccounts = bankAccounts;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CheckRecord check, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("CheckRecord_Checkbox_SummaryAdmin", check)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("CheckRecord_Fields_SummaryAdmin", check)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("CheckRecord_Description_SummaryAdmin", check)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Payee:5"),
            View("CheckRecord_Tags_SummaryAdmin", check)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("CheckRecord_ActionsMenu_SummaryAdmin", check).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(CheckRecord check, BuildEditorContext context)
        => await EditorAsync(check, await _addressEditor.BuildAsync(check.PayeeAddress, AddressOptions(check.PayeeAddress?.Country)));

    // The editor, with the payee's address as stored or as posted.
    private async Task<IDisplayResult> EditorAsync(CheckRecord check, AddressEditorViewModel payeeAddress)
    {
        var accounts = await BusinessAccountsAsync();
        var addressEditor = await _shapeFactory.CreateAddressEditorAsync(payeeAddress, $"{Prefix}.{nameof(CheckRecordViewModel.PayeeAddress)}");

        // A new check is dated today in the site's time zone, never the UTC date.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return Initialize<CheckRecordViewModel>("CheckRecordFields_Edit", model =>
        {
            model.BankAccountId = check.BankAccountId;
            model.PayeeName = check.PayeeName;
            model.PayeeAddress = payeeAddress;
            model.PayeeAddressEditor = addressEditor;
            model.Amount = check.Amount > 0 ? check.Amount.ToString(CultureInfo.InvariantCulture) : null;
            model.CheckDate = (check.CheckDate == default ? today : check.CheckDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            model.Memo = check.Memo;
            model.Numbering = check.Numbering;
            model.Number = check.Numbering == CheckNumbering.Handwritten && check.Number > 0 ? check.Number.ToString(CultureInfo.InvariantCulture) : null;
            model.NumberingOptions =
            [
                new SelectListItem(S["The account's next number"], nameof(CheckNumbering.Next), check.Numbering == CheckNumbering.Next),
                new SelectListItem(S["Number it when it prints on pre-printed checks"], nameof(CheckNumbering.AtPrint), check.Numbering == CheckNumbering.AtPrint),
                new SelectListItem(S["Already written by hand"], nameof(CheckNumbering.Handwritten), check.Numbering == CheckNumbering.Handwritten),
            ];
            model.BankAccountOptions = accounts
                .Select(account => new SelectListItem($"{account.Name} ({BankAccountNumbers.Masked(account)})", account.ItemId, account.ItemId == check.BankAccountId))
                .ToList();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(CheckRecord check, UpdateEditorContext context)
    {
        var model = new CheckRecordViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        check.BankAccountId = model.BankAccountId;
        check.PayeeName = string.IsNullOrWhiteSpace(model.PayeeName) ? null : model.PayeeName.Trim();
        check.Payee = default(PartyRef);
        // The payee's address is optional; one that is entered is checked by its country's format.
        model.PayeeAddress ??= new AddressEditorViewModel();

        if (!model.PayeeAddress.HasAnyValue())
        {
            check.PayeeAddress = null;
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(model.PayeeAddress, context.Updater.ModelState, $"{Prefix}.{nameof(model.PayeeAddress)}", AddressOptions(model.PayeeAddress.Country));

            if (result.IsValid)
            {
                check.PayeeAddress = result.Address;
            }
        }

        await _addressEditor.PopulateAsync(model.PayeeAddress, AddressOptions(model.PayeeAddress.Country));
        check.Memo = string.IsNullOrWhiteSpace(model.Memo) ? null : model.Memo.Trim();
        check.Numbering = model.Numbering;
        check.Number = 0;

        if (model.Numbering == CheckNumbering.Handwritten)
        {
            if (long.TryParse(model.Number?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
            {
                check.Number = number;
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Number), S["Enter the number written on the check."]);
            }
        }

        if (decimal.TryParse(model.Amount?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
        {
            check.Amount = amount;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Amount), S["Enter the amount with digits and a period, such as 125.00."]);
        }

        if (DateOnly.TryParseExact(model.CheckDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            check.CheckDate = date;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CheckDate), S["Enter the check date."]);
        }

        return await EditorAsync(check, model.PayeeAddress);
    }

    private static AddressEditorOptions AddressOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };

    private async Task<IReadOnlyList<BankAccount>> BusinessAccountsAsync()
        => (await _bankAccounts.ListByOwnersAsync([default]))
            .Where(account => account.Status is not (BankAccountStatus.Closed or BankAccountStatus.Failed))
            .ToList();
}
