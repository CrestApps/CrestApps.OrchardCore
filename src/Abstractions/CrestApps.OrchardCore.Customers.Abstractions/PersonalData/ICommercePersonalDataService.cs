using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// Exports and anonymizes the personal data the commerce modules keep about one person, for access and erasure
/// requests. The person is named by their owner (an account, or a guest owner id), by their email, or by both. Each
/// enabled commerce module adds its own records through an <see cref="ICommercePersonalDataContributor"/>, so a module
/// that is not enabled is skipped.
/// </summary>
public interface ICommercePersonalDataService
{
    /// <summary>
    /// Returns everything the enabled commerce modules hold about the person: their transactions, payments, refunds,
    /// installment plans, subscriptions, checkout sessions, coupon redemptions and payment-provider customers.
    /// </summary>
    /// <param name="owner">The person's account or guest owner, or <see langword="null"/> to find them by email only.</param>
    /// <param name="email">The person's email, or <see langword="null"/> to find them by owner only.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentException">Neither an owner nor an email is given.</exception>
    Task<CommercePersonalDataExport> ExportAsync(CustomerOwner owner, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the person's names, emails, phone numbers, addresses, IP addresses, user agents, saved checkout steps
    /// and card descriptions from the commerce records that hold them. Amounts, dates, statuses and invoice and receipt
    /// numbers are kept, because the business must retain them.
    /// </summary>
    /// <param name="owner">The person's account or guest owner, or <see langword="null"/> to find them by email only.</param>
    /// <param name="email">The person's email, or <see langword="null"/> to find them by owner only.</param>
    /// <param name="replacementText">
    /// The text a removed name is replaced with, for example <c>Deleted customer</c>, or <see langword="null"/> to clear
    /// it. Emails, phone numbers, IP addresses and user agents are always cleared.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentException">Neither an owner nor an email is given.</exception>
    Task<CommercePersonalDataAnonymizeResult> AnonymizeAsync(CustomerOwner owner, string email, string replacementText, CancellationToken cancellationToken = default);
}
