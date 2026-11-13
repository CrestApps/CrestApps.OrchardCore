using System.Globalization;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Disbursements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Disbursements.Drivers;

/// <summary>
/// The rows of the list of disbursements, and the editor of one written by hand. The editor fills an unsaved
/// disbursement; the controller records it through the disbursement rules.
/// </summary>
internal sealed class DisbursementDisplayDriver : DisplayDriver<Disbursement>
{
    private readonly IBankAccountManager _bankAccounts;
    private readonly IDisbursementService _disbursementService;

    internal readonly IStringLocalizer S;

    public DisbursementDisplayDriver(IBankAccountManager bankAccounts, IDisbursementService disbursementService, IStringLocalizer<DisbursementDisplayDriver> stringLocalizer)
    {
        _bankAccounts = bankAccounts;
        _disbursementService = disbursementService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(Disbursement disbursement, BuildDisplayContext context)
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
            View("Disbursement_Checkbox_SummaryAdmin", disbursement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("Disbursement_Fields_SummaryAdmin", disbursement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Payee:1"),
            View("Disbursement_Description_SummaryAdmin", disbursement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("Disbursement_Tags_SummaryAdmin", disbursement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Disbursement_ActionsMenu_SummaryAdmin", disbursement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "ActionsMenu:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "ActionsMenu:5"));
    }

    public override async Task<IDisplayResult> EditAsync(Disbursement disbursement, BuildEditorContext context)
    {
        var business = (await _bankAccounts.ListByOwnersAsync([default]))
            .Where(account => account.Status is not (BankAccountStatus.Closed or BankAccountStatus.Failed))
            .OrderBy(account => account.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var payees = (await _bankAccounts.GetAllAsync())
            .Where(account => !account.Owner.IsEmpty && account.CanReceivePayments())
            .OrderBy(account => account.HolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Initialize<DisbursementViewModel>("DisbursementFields_Edit", model =>
        {
            model.Method = disbursement.Method;
            model.SourceBankAccountId = disbursement.SourceBankAccountId;
            model.DestinationBankAccountId = disbursement.DestinationBankAccountId;
            model.PayeeName = disbursement.PayeeName;
            model.IsBusinessPayee = disbursement.IsBusinessPayee;
            model.Amount = disbursement.Amount > 0 ? disbursement.Amount.ToString(CultureInfo.InvariantCulture) : null;
            model.Memo = disbursement.Memo;
            model.MethodOptions = _disbursementService.Providers
                .Select(provider => new SelectListItem(provider.DisplayName, provider.Method, provider.Method == disbursement.Method))
                .ToList();
            model.SourceAccountOptions = business
                .Select(account => new SelectListItem($"{account.Name} ({BankAccountNumbers.Masked(account)})", account.ItemId, account.ItemId == disbursement.SourceBankAccountId))
                .ToList();
            model.DestinationAccountOptions =
            [
                new SelectListItem(S["None"], string.Empty),
                .. payees.Select(account => new SelectListItem($"{account.HolderName} · {account.Name} ({BankAccountNumbers.Masked(account)})", account.ItemId, account.ItemId == disbursement.DestinationBankAccountId)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Disbursement disbursement, UpdateEditorContext context)
    {
        var model = new DisbursementViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        disbursement.Method = model.Method;
        disbursement.SourceBankAccountId = Clean(model.SourceBankAccountId);
        disbursement.DestinationBankAccountId = Clean(model.DestinationBankAccountId);
        disbursement.PayeeName = Clean(model.PayeeName);
        disbursement.IsBusinessPayee = model.IsBusinessPayee;
        disbursement.Memo = Clean(model.Memo);

        if (decimal.TryParse(model.Amount?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
        {
            disbursement.Amount = amount;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Amount), S["Enter the amount, such as 1250.00."]);
        }

        // A payee picked by bank account is named as the bank knows them, unless a name was typed.
        if (disbursement.DestinationBankAccountId is { } accountId && await _bankAccounts.FindByIdAsync(accountId) is { } account)
        {
            disbursement.Payee = account.Owner;
            disbursement.PayeeName ??= account.HolderName;
        }

        return await EditAsync(disbursement, context);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
