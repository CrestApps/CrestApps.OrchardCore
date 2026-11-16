using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// The rules of bank accounts: how numbers are checked and protected, which status moves are allowed, and who is told.
/// It checks no permission; the caller does.
/// </summary>
public interface IBankAccountService
{
    /// <summary>
    /// Gets the verifiers the enabled features registered.
    /// </summary>
    IReadOnlyList<IBankAccountVerifier> Verifiers { get; }

    /// <summary>
    /// Gets the link providers the enabled features registered. Ask each whether it is available before showing it.
    /// </summary>
    IReadOnlyList<IBankAccountLinkProvider> LinkProviders { get; }

    /// <summary>
    /// Writes a linked account's numbers and status on an account that is not saved yet: the numbers go through
    /// <see cref="ApplyNumbers"/>, so a number that differs from the stored one is recorded as changed, then the account
    /// moves to the result's status with the provider as its <see cref="BankAccount.VerificationMethod"/>. The kind and
    /// the bank's name are filled in when the result has them.
    /// </summary>
    /// <param name="account">The account, with its <see cref="BankAccount.Country"/> set.</param>
    /// <param name="providerName">The link provider's name.</param>
    /// <param name="result">What the provider read from the bank.</param>
    /// <returns>Why the result was refused; empty when it was applied.</returns>
    IReadOnlyList<BankAccountNumberError> ApplyLink(BankAccount account, string providerName, BankAccountLinkResult result);

    /// <summary>
    /// Checks and stores a routing number and an account number on an account that is not saved yet. A blank value
    /// keeps the stored one. A number that differs from the stored one takes the account back to
    /// <see cref="BankAccountStatus.Unverified"/>, and the save tells the observers.
    /// </summary>
    /// <param name="account">The account, with its <see cref="BankAccount.Country"/> set.</param>
    /// <param name="routingNumber">The routing number as entered, or blank to keep the stored one.</param>
    /// <param name="accountNumber">The account number as entered, or blank to keep the stored one.</param>
    /// <returns>The numbers that were refused, and why; empty when both were accepted.</returns>
    IReadOnlyList<BankAccountNumberError> ApplyNumbers(BankAccount account, string routingNumber, string accountNumber);

    /// <summary>
    /// Moves an account that is not saved yet to another status, when <see cref="BankAccountStatuses.Transitions"/>
    /// allows it. The save tells the observers.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="status">The new status.</param>
    /// <param name="reason">Why, or <see langword="null"/>.</param>
    /// <param name="verificationMethod">The verifier that decided, or <see langword="null"/> when a person did.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    bool TrySetStatus(BankAccount account, BankAccountStatus status, string reason = null, string verificationMethod = null);

    /// <summary>
    /// Moves a saved account to another status and saves it, when the move is allowed.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="status">The new status.</param>
    /// <param name="reason">Why, or <see langword="null"/>.</param>
    /// <param name="verificationMethod">The verifier that decided, or <see langword="null"/> when a person did.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> ChangeStatusAsync(BankAccount account, BankAccountStatus status, string reason = null, string verificationMethod = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts checking a saved account with a verifier, and saves the status it moves to.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="verifierName">The verifier's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What the verifier did.</returns>
    Task<BankAccountVerificationResult> StartVerificationAsync(BankAccount account, string verifierName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the full account number of an account, writes a log entry (the account and who read it, never the number)
    /// and tells the observers that it was shown, so the Audit Trail records who saw it. Call it only for a user who holds <see cref="BankingPermissions.RevealBankAccountNumbers"/>,
    /// or for a file a payment needs.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="reason">Why it is read, such as "Shown on the account page" or "Written to an ACH file".</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The account number, or <see langword="null"/> when none is stored or it can't be read with the tenant's keys.</returns>
    Task<string> RevealAccountNumberAsync(BankAccount account, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns why records of other modules point to an account, such as checks drawn on it. An account in use can't
    /// be deleted: close it instead.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reasons; empty when nothing points to the account.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(BankAccount account, CancellationToken cancellationToken = default);
}
