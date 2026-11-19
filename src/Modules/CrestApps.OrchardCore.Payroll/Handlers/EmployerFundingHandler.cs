using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Handlers;

/// <summary>
/// An employer pays from one of the business's own bank accounts (no owner) that is open: on every save, the editor's
/// and a recipe's alike.
/// </summary>
internal sealed class EmployerFundingHandler : CatalogEntryHandlerBase<Employer>
{
    private readonly IBankAccountManager _accounts;

    internal readonly IStringLocalizer S;

    public EmployerFundingHandler(IBankAccountManager accounts, IStringLocalizer<EmployerFundingHandler> stringLocalizer)
    {
        _accounts = accounts;
        S = stringLocalizer;
    }

    public override async Task ValidatingAsync(ValidatingContext<Employer> context, CancellationToken cancellationToken = default)
    {
        var employer = context.Model;

        if (string.IsNullOrEmpty(employer?.FundingBankAccountId))
        {
            return;
        }

        if (await _accounts.FindByIdAsync(employer.FundingBankAccountId, cancellationToken) is not { } account
            || !account.Owner.IsEmpty
            || account.Status is BankAccountStatus.Closed or BankAccountStatus.Failed)
        {
            context.Result.Fail(new ValidationResult(S["Choose one of the business's own bank accounts that is open."], [nameof(Employer.FundingBankAccountId)]));
        }
    }
}
