using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The bank account an employer pays from, chosen among the accounts the employer owns.
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
        var owned = string.IsNullOrEmpty(employer.ItemId)
            ? []
            : await _accounts.ListByOwnersAsync([new PartyRef(PayrollConstants.EmployerPartyKind, employer.ItemId)]);

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

        if (string.IsNullOrEmpty(model.FundingBankAccountId))
        {
            employer.FundingBankAccountId = null;
        }
        else if (await _accounts.FindByIdAsync(model.FundingBankAccountId) is { } account
            && account.Owner is { } owner && owner.Kind == PayrollConstants.EmployerPartyKind && owner.Id == employer.ItemId)
        {
            employer.FundingBankAccountId = account.ItemId;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.FundingBankAccountId), S["Choose one of the employer's own bank accounts."]);
        }

        return await EditAsync(employer, context);
    }
}
