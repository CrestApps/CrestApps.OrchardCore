using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Handlers;

/// <summary>
/// With Banking on, every deposit of a net pay split goes to the person's own bank account, owned by their staff member
/// or their user: on every save, the editor's, My Pay's and a recipe's alike. Nobody can route someone's pay to their
/// own account or a vendor's.
/// </summary>
internal sealed class PayProfileAccountHandler : CatalogEntryHandlerBase<EmployeePayProfile>
{
    private readonly IBankAccountManager _accounts;
    private readonly IStaffDirectory _staff;

    internal readonly IStringLocalizer S;

    public PayProfileAccountHandler(IBankAccountManager accounts, IStaffDirectory staff, IStringLocalizer<PayProfileAccountHandler> stringLocalizer)
    {
        _accounts = accounts;
        _staff = staff;
        S = stringLocalizer;
    }

    public override async Task ValidatingAsync(ValidatingContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
    {
        var profile = context.Model;

        if (profile?.Distribution is not { Count: > 0 })
        {
            return;
        }

        foreach (var row in profile.Distribution.Where(row => row.Method == NetPayMethod.BankAccount && !string.IsNullOrEmpty(row.BankAccountId)))
        {
            var account = await _accounts.FindByIdAsync(row.BankAccountId, cancellationToken);

            if (!await PayAccountOwnership.IsOwnedByAsync(account, profile.StaffMemberId, _staff, cancellationToken))
            {
                context.Result.Fail(new ValidationResult(S["Net pay goes only to the person's own bank accounts."], [nameof(EmployeePayProfile.Distribution)]));

                return;
            }
        }
    }
}
