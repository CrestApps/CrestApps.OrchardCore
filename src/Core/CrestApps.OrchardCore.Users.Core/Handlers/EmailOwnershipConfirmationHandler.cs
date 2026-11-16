using OrchardCore.Modules;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Records the address as proved (<see cref="UserEmailOwnership"/>) when a user follows the link e-mailed to confirm
/// it: the link is a round trip to the mailbox. With <see cref="Services.EmailBoundConfirmationTokenProvider"/> in
/// place, a link only confirms the address it was sent to.
/// </summary>
/// <remarks>
/// The marker is saved through the session rather than the user manager: Orchard Core's user store builds these
/// handlers, so a handler that needed the user manager would need the store that builds it. The user was just saved by
/// the confirmation, and the request's session saves this change with it.
/// </remarks>
public sealed class EmailOwnershipConfirmationHandler : UserEventHandlerBase
{
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOwnershipConfirmationHandler"/> class.
    /// </summary>
    /// <param name="session">The request's session.</param>
    /// <param name="clock">The clock.</param>
    public EmailOwnershipConfirmationHandler(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override async Task ConfirmedAsync(UserConfirmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ConfirmationType == UserConfirmationType.Email &&
            context.User is User user &&
            !UserEmailOwnership.IsVerified(user.Properties, user.Email))
        {
            UserEmailOwnership.MarkVerified(user.Properties, user.Email, _clock.UtcNow);
            await _session.SaveAsync(user);
        }
    }
}
