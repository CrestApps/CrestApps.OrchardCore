using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Decides whether a contact may be reached on a channel, taking into account everybody reachable at their numbers.
/// </summary>
/// <remarks>
/// A contact's own preference is not the whole answer. The same person is often on file more than once, and a
/// request to stop recorded against one record has to stop the others too: live, a contact who had asked not to be
/// called shared a number with a second record, and the second record was dialled at its other number minutes
/// later. Whoever asked to stop may not be reached at any number that leads to them.
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
}
