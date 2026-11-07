using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Decides whether a contact may be reached on a channel, taking into account everybody reachable at their numbers,
/// and keeps the opt-outs recorded against an address that no contact holds.
/// </summary>
/// <remarks>
/// A contact's own preference is not the whole answer. The same person is often on file more than once, and a
/// request to stop recorded against one record has to stop the others too: live, a contact who had asked not to be
/// called shared a number with a second record, and the second record was dialled at its other number minutes
/// later. Whoever asked to stop may not be reached at any number that leads to them. A person who asks to stop from
/// a number no record holds is owed the same, so that opt-out is recorded against the address itself.
/// </remarks>
public interface IContactOptOutResolver
{
    /// <summary>
    /// Whether this contact, or anybody sharing one of their phone numbers, has asked not to be reached on this channel.
    /// </summary>
    /// <param name="contact">The contact about to be reached, or <see langword="null"/> when there is none.</param>
    /// <param name="channel">The channel they would be reached on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> HasOptedOutAsync(ContentItem contact, string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether somebody asked not to be reached on this channel at this address, whatever contact holds it.
    /// </summary>
    /// <param name="channel">The channel they would be reached on.</param>
    /// <param name="address">The address they would be reached at, for example a phone number.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> HasAddressOptedOutAsync(string channel, string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that somebody asked not to be reached on a channel at an address. Recording an address that is already
    /// opted out keeps one record.
    /// </summary>
    /// <param name="optOut">The opt-out to record. Its <see cref="ContactAddressOptOut.Channel"/> and <see cref="ContactAddressOptOut.Address"/> are required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAddressOptOutAsync(ContactAddressOptOut optOut, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the opt-out recorded against an address, when somebody opts back in.
    /// </summary>
    /// <param name="channel">The channel the opt-out applies to.</param>
    /// <param name="address">The address the opt-out was recorded against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when an opt-out was removed.</returns>
    Task<bool> ClearAddressOptOutAsync(string channel, string address, CancellationToken cancellationToken = default);
}
