using CrestApps.OrchardCore.Banking.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// A way for a person to link their bank: they sign in to it in the browser, pick the account, and the account is saved
/// with the bank's own numbers, usually verified at once. Register one with
/// <see cref="BankingServiceCollectionExtensions.AddBankAccountLinkProvider{TProvider}"/>.
/// </summary>
/// <remarks>
/// Banking owns the pages. It calls <see cref="StartAsync"/>, renders the shape the start names inside a form that has
/// a hidden <c>clientResult</c> field, and the shape's script fills that field and submits the form when the person has
/// linked their bank. Banking then calls <see cref="CompleteAsync"/> with the field's value, applies the result with
/// <see cref="IBankAccountService.ApplyLink"/>, saves the account, and calls <see cref="FinishAsync"/> either way.
/// </remarks>
public interface IBankAccountLinkProvider
{
    /// <summary>
    /// Gets the provider's name, stored as the account's <see cref="BankAccount.VerificationMethod"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown on the link buttons.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Returns whether the provider is set up for this site, so its buttons show.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when a person can link a bank with it.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares the browser part of a link.
    /// </summary>
    /// <param name="context">The account and the person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shape to render and what it needs, or why linking can't start.</returns>
    Task<BankAccountLinkStart> StartAsync(BankAccountLinkContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the account the person linked.
    /// </summary>
    /// <param name="context">The account and the person.</param>
    /// <param name="clientResult">What the browser part posted.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The account's numbers and status, or why it could not be linked.</returns>
    Task<BankAccountLinkResult> CompleteAsync(BankAccountLinkContext context, string clientResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps or lets go of what the provider holds for a link once Banking has tried to save the account.
    /// </summary>
    /// <param name="context">The account and the person.</param>
    /// <param name="result">The result <see cref="CompleteAsync"/> returned.</param>
    /// <param name="saved">Whether the account was saved with the result.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task FinishAsync(BankAccountLinkContext context, BankAccountLinkResult result, bool saved, CancellationToken cancellationToken = default);
}
