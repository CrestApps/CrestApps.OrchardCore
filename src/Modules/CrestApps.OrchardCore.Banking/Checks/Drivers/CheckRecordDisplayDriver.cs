using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The rows of the check register, and the editor of a check written by hand. The editor fills an unsaved check; the
/// controller issues it through the check rules, which give it its number.
/// </summary>
internal sealed class CheckRecordDisplayDriver : DisplayDriver<CheckRecord>
{
    private readonly IBankAccountManager _bankAccounts;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public CheckRecordDisplayDriver(IBankAccountManager bankAccounts, ILocalClock localClock, IStringLocalizer<CheckRecordDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _bankAccounts = bankAccounts;
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
    {
        var accounts = await BusinessAccountsAsync();

        // A new check is dated today in the site's time zone, never the UTC date.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        return Initialize<CheckRecordViewModel>("CheckRecordFields_Edit", model =>
        {
            model.BankAccountId = check.BankAccountId;
            model.PayeeName = check.PayeeName;
            model.PayeeAddressLine1 = check.PayeeAddress?.AddressLine1;
            model.PayeeAddressLine2 = check.PayeeAddress?.AddressLine2;
            model.PayeeCity = check.PayeeAddress?.City;
            model.PayeeRegion = check.PayeeAddress?.Region;
            model.PayeePostalCode = check.PayeeAddress?.PostalCode;
            model.Amount = check.Amount > 0 ? check.Amount.ToString(CultureInfo.InvariantCulture) : null;
            model.CheckDate = (check.CheckDate == default ? today : check.CheckDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            model.Memo = check.Memo;
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
        check.PayeeAddress = string.IsNullOrWhiteSpace(model.PayeeAddressLine1) && string.IsNullOrWhiteSpace(model.PayeeCity)
            ? null
            : new Address
            {
                AddressLine1 = model.PayeeAddressLine1?.Trim(),
                AddressLine2 = model.PayeeAddressLine2?.Trim(),
                City = model.PayeeCity?.Trim(),
                Region = model.PayeeRegion?.Trim(),
                PostalCode = model.PayeePostalCode?.Trim(),
                Country = "US",
            };
        check.Memo = string.IsNullOrWhiteSpace(model.Memo) ? null : model.Memo.Trim();

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

        return await EditAsync(check, context);
    }

    private async Task<IReadOnlyList<BankAccount>> BusinessAccountsAsync()
        => (await _bankAccounts.ListByOwnersAsync([default]))
            .Where(account => account.Status is not (BankAccountStatus.Closed or BankAccountStatus.Failed))
            .ToList();
}
