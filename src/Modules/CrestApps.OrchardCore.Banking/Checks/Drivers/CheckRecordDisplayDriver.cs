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
/// The rows of the check register, and the editor of a check written in the admin or edited while it waits to print.
/// A new check's editor fills an unsaved check, with how it gets its number (and the number of a check written by hand);
/// the controller issues it through the check rules. An edited check keeps its bank account and number: only its payee,
/// address, amount, date and memo change. Other modules add their own fields with their own drivers on
/// <see cref="CheckRecord"/>.
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
        => await EditorAsync(check, await _addressEditor.BuildAsync(check.PayeeAddress, AddressOptions(check.PayeeAddress?.Country)), context.IsNew);

    // The editor, with the payee's address as stored or as posted.
    private async Task<IDisplayResult> EditorAsync(CheckRecord check, AddressEditorViewModel payeeAddress, bool isNew)
    {
        var accounts = await BusinessAccountsAsync();
        var addressEditor = await _shapeFactory.CreateAddressEditorAsync(payeeAddress, $"{Prefix}.{nameof(CheckRecordViewModel.PayeeAddress)}");

        // A new check is dated today in the site's time zone, never the UTC date.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        void Populate(CheckRecordViewModel model)
        {
            model.IsNew = isNew;
            model.BankAccountId = check.BankAccountId;
            model.BankAccountName = check.BankAccountName;
            model.CheckNumber = check.Number;
            model.PayeeName = check.PayeeName;
            model.PayeeAddress = payeeAddress;
            model.PayeeAddressEditor = addressEditor;
            model.Amount = check.Amount > 0 ? check.Amount : null;
            model.Currency = check.Currency;
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
        }

        return Combine(
            Initialize<CheckRecordViewModel>("CheckRecordFields_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["The Check"], "10")),
            Initialize<CheckRecordViewModel>("CheckRecordPayee_Edit", Populate)
                .Location(l => l.Zone("Content", "2").Card(S["Payee's Address"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(CheckRecord check, UpdateEditorContext context)
    {
        var model = new CheckRecordViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // An edited check keeps its bank account, its number and the party it pays; a new one is written to a name.
        if (context.IsNew)
        {
            check.BankAccountId = model.BankAccountId;
            check.Payee = default(PartyRef);
        }

        check.PayeeName = string.IsNullOrWhiteSpace(model.PayeeName) ? null : model.PayeeName.Trim();
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

        if (context.IsNew)
        {
            check.Numbering = model.Numbering;
            check.Number = 0;
        }

        if (context.IsNew && model.Numbering == CheckNumbering.Handwritten)
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

        // An amount that can't be read is also a model error from the binder, so it is never saved as another number.
        if (model.Amount is decimal amount && amount > 0m)
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

        return await EditorAsync(check, model.PayeeAddress, context.IsNew);
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
