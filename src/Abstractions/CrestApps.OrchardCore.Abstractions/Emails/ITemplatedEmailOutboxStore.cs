using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// Removes emails kept by <see cref="ITemplatedEmailOutbox"/>, sent or still waiting, for example when the person they
/// went to asks to be forgotten: a waiting email would still reach them, and a sent one keeps their address and words.
/// </summary>
public interface ITemplatedEmailOutboxStore
{
    /// <summary>
    /// Removes the emails of a record (<see cref="TemplatedEmail.Source"/>), in the caller's unit of work.
    /// </summary>
    /// <param name="source">The record.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many emails were removed.</returns>
    Task<int> DeleteBySourceAsync(RecordRef source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the emails to an address, compared as <see cref="EmailAddresses.Normalize"/> writes it,
    /// in the caller's unit of work.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many emails were removed.</returns>
    Task<int> DeleteByRecipientAsync(string email, CancellationToken cancellationToken = default);
}
