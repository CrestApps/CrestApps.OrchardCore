using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Keeps work that was offered to a user with them while they are still working on it.
/// </summary>
/// <remarks>
/// A preview record is offered to an agent for a limited time, and the activity screen is where they review it, update
/// the contact and choose a disposition. A feature that offers work registers an implementation so that screen can say
/// the agent is still there, and the offer does not run out under them and go to someone else.
/// </remarks>
public interface IActivityOfferKeepAlive
{
    /// <summary>
    /// Extends the offer of the activity to the user, when it is still offered to them.
    /// </summary>
    /// <param name="activity">The activity the user is working on.</param>
    /// <param name="userId">The user working on it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When the offer now runs out, or <see langword="null"/> when nothing was offered to the user.</returns>
    Task<DateTime?> KeepAliveAsync(OmnichannelActivity activity, string userId, CancellationToken cancellationToken = default);
}
