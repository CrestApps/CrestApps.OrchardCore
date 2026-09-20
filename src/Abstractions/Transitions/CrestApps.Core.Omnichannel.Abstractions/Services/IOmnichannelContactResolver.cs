using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Finds the contact behind an identifier or a way of reaching them.
/// </summary>
/// <remarks>
/// The lookups a live interaction needs: an inbound call or message arrives with a number, and work
/// already under way arrives with an identifier.
/// </remarks>
public interface IOmnichannelContactResolver
{
    /// <summary>
    /// Finds a contact by identifier.
    /// </summary>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contact, or <see langword="null"/> when there is no such contact.</returns>
    Task<OmnichannelContact> FindByIdAsync(string contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the contacts reachable on a phone number.
    /// </summary>
    /// <remarks>
    /// Several, because one number can belong to more than one person - a household, or a switchboard -
    /// and the caller decides what to do about that rather than being handed an arbitrary one.
    /// </remarks>
    /// <param name="phoneNumber">The number, in E.164 form.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contacts, which may be empty.</returns>
    Task<IReadOnlyCollection<OmnichannelContact>> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the contacts reachable at an email address.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contacts, which may be empty.</returns>
    Task<IReadOnlyCollection<OmnichannelContact>> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
