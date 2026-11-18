using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Finds the agreements tickets work under, when agreements are on: the one that covers a new ticket of an account, and
/// an agreement by id. Without it, tickets have no agreement.
/// </summary>
public interface ITicketAgreementResolver
{
    /// <summary>
    /// Finds the agreement in force that covers a new ticket: the account's agreement whose ticket terms name the desk and
    /// ticket type (or every desk and type), the earliest when several do.
    /// </summary>
    /// <param name="accountId">The ticket's account.</param>
    /// <param name="deskId">The ticket's desk.</param>
    /// <param name="contentType">The ticket type.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreement, or <see langword="null"/>.</returns>
    Task<TicketAgreementInfo> FindForNewTicketAsync(string accountId, string deskId, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an agreement that covers tickets, by id.
    /// </summary>
    /// <param name="agreementId">The agreement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreement, or <see langword="null"/>.</returns>
    Task<TicketAgreementInfo> FindAsync(string agreementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the agreements of an account in force that cover tickets, to choose from.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreements.</returns>
    Task<IReadOnlyList<TicketAgreementInfo>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default);
}
