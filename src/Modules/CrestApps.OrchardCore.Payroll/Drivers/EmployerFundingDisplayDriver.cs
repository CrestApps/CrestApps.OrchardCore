using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The bank account an employer pays from: one of the business's own accounts (an account with no owner), the accounts
/// Disbursements sends money from and Checks draws checks on, and whose bank file settings are kept.
/// </summary>
internal sealed class EmployerFundingDisplayDriver : DisplayDriver<Employer>
{
    private readonly IBankAccountManager _accounts;

    internal readonly IStringLocalizer S;

    public EmployerFundingDisplayDriver(IBankAccountManager accounts, IStringLocalizer<EmployerFundingDisplayDriver> stringLocalizer)
    {
        _accounts = accounts;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(Employer employer, BuildEditorContext context)
    {
        var owned = (await _accounts.ListByOwnersAsync([default]))
            .Where(account => account.Status is not (BankAccountStatus.Closed or BankAccountStatus.Failed) || account.ItemId == employer.FundingBankAccountId)
            .OrderBy(account => account.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Initialize<EmployerFundingViewModel>("EmployerFunding_Edit", model =>
        {
            model.FundingBankAccountId = employer.FundingBankAccountId;
            model.Accounts = [.. owned.Select(account => new SelectListItem(account.Name, account.ItemId, account.ItemId == employer.FundingBankAccountId))];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(Employer employer, UpdateEditorContext context)
    {
        var model = new EmployerFundingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Which accounts may fund pay is EmployerFundingHandler's rule, as for a recipe.
        employer.FundingBankAccountId = string.IsNullOrEmpty(model.FundingBankAccountId) ? null : model.FundingBankAccountId;

        return await EditAsync(employer, context);
    }
}
