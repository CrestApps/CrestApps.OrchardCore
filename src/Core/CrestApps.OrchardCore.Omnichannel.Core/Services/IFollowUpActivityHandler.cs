using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Takes part in creating the activity that tries a contact again after a completed one, before it is saved.
/// </summary>
/// <remarks>
/// A subject's "Try again" action creates the next attempt as a new activity. Whatever carried the first attempt -- a
/// dialer campaign's queue, for example -- has to take the new one in as well, or it is never worked: a handler can set
/// when it is due, give it back to the system that carries it, or refuse it when no attempt is left.
/// </remarks>
public interface IFollowUpActivityHandler
{
    /// <summary>
    /// Called before the follow-up activity is saved.
    /// </summary>
    /// <param name="context">The completed activity, the follow-up and how it is being created.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreatingAsync(FollowUpActivityContext context, CancellationToken cancellationToken = default);
}
