using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Creates a contact, and records what an interaction learned about one.
/// </summary>
public interface IOmnichannelContactWriter
{
    /// <summary>
    /// Creates a contact.
    /// </summary>
    /// <param name="contact">The contact to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contact as stored, with its identifier.</returns>
    Task<OmnichannelContact> CreateAsync(OmnichannelContact contact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a change set to a contact.
    /// </summary>
    /// <remarks>
    /// A change set rather than a whole contact, so a conversation recording one thing it learned
    /// cannot revert everything else on the record from a stale read.
    /// </remarks>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="changes">What to change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the contact changed, and <see langword="false"/> when it already
    /// said this.
    /// </returns>
    Task<bool> ApplyAsync(string contactId, OmnichannelContactChanges changes, CancellationToken cancellationToken = default);
}
