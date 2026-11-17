using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// With Banking on, a deposit to a bank account that is missing, not the person's own or not verified keeps a profile
/// from being paid.
/// </summary>
internal sealed class BankAccountPayBlockers : IPayProfileBlockerProvider
{
    private readonly IBankAccountManager _accounts;
    private readonly IStaffDirectory _staff;

    internal readonly IStringLocalizer S;

    public BankAccountPayBlockers(IBankAccountManager accounts, IStaffDirectory staff, IStringLocalizer<BankAccountPayBlockers> stringLocalizer)
    {
        _accounts = accounts;
        _staff = staff;
        S = stringLocalizer;
    }

    public async Task AddBlockersAsync(EmployeePayProfile profile, IList<PayBlocker> blockers, CancellationToken cancellationToken = default)
    {
        foreach (var row in profile.Distribution.Where(row => row.Method == NetPayMethod.BankAccount && !string.IsNullOrEmpty(row.BankAccountId)))
        {
            var account = await _accounts.FindByIdAsync(row.BankAccountId, cancellationToken);

            if (!await PayAccountOwnership.IsOwnedByAsync(account, profile.StaffMemberId, _staff, cancellationToken))
            {
                blockers.Add(new PayBlocker("BankAccountMissing", S["A deposit goes to a bank account that is gone or isn't the person's."]));
            }
            else if (account.Status != BankAccountStatus.Verified)
            {
                blockers.Add(new PayBlocker("BankAccountUnverified", S["The bank account {0} isn't verified yet.", account.Name]));
            }
        }
    }
}
