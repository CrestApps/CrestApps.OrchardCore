using CrestApps.OrchardCore.Banking.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// A way to check that a bank account is right, such as a zero-amount test entry the bank must accept. Register one
/// with <see cref="BankingServiceCollectionExtensions.AddBankAccountVerifier{TVerifier}"/>. A verifier that waits for
/// an answer moves the account to <see cref="BankAccountStatus.VerificationSent"/>, and moves it on through
/// <see cref="IBankAccountService.ChangeStatusAsync"/> when the answer comes.
/// </summary>
public interface IBankAccountVerifier
{
    /// <summary>
    /// Gets the verifier's name, stored on the accounts it checks.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to the person who chooses how to check an account.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Starts checking an account.
    /// </summary>
    /// <param name="account">The account, already saved.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What the verifier did.</returns>
    Task<BankAccountVerificationResult> StartAsync(BankAccount account, CancellationToken cancellationToken = default);
}
