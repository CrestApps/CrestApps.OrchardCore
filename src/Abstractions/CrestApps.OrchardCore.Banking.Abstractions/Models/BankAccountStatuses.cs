using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// The moves a bank account's status may make.
/// </summary>
public static class BankAccountStatuses
{
    /// <summary>
    /// Gets the allowed moves. A changed number always takes an account back to
    /// <see cref="BankAccountStatus.Unverified"/>; <see cref="BankAccountStatus.Closed"/> is final.
    /// </summary>
    public static TransitionTable<BankAccountStatus> Transitions { get; } = new(new Dictionary<BankAccountStatus, BankAccountStatus[]>
    {
        [BankAccountStatus.Unverified] = [BankAccountStatus.VerificationSent, BankAccountStatus.Verified, BankAccountStatus.Failed, BankAccountStatus.Closed],
        [BankAccountStatus.VerificationSent] = [BankAccountStatus.Unverified, BankAccountStatus.Verified, BankAccountStatus.Failed, BankAccountStatus.Closed],
        [BankAccountStatus.Verified] = [BankAccountStatus.Unverified, BankAccountStatus.Failed, BankAccountStatus.Closed],
        [BankAccountStatus.Failed] = [BankAccountStatus.Unverified, BankAccountStatus.VerificationSent, BankAccountStatus.Verified, BankAccountStatus.Closed],
        [BankAccountStatus.Closed] = [],
    });
}
