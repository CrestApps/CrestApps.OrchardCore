using CrestApps.OrchardCore.Users.Core.Services;
using OrchardCore.Modules;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Records the address as proved (<see cref="UserEmailOwnership"/>) when a user follows the link e-mailed to confirm
/// it, on an account with no password, no external login and no passkey: the link is a round trip to the mailbox. With
/// <see cref="Services.EmailBoundConfirmationTokenProvider"/> in place, a link only confirms the address it was sent to.
/// </summary>
/// <remarks>
/// <para>
/// The link proves who reads the mail, not who set the account's password or linked its external logins: someone can
/// register with another person's address and a password of their own, and that person may follow the "confirm your
/// account" mail. Showing one credential doesn't settle it either, since a password can be set again with the mailbox
/// alone while somebody else's login stays. So an account with credentials is never marked here: Orchard Core still
/// confirms the address, but nothing that belongs to it is handed over until the owner confirms on the "Confirm it's
/// you" page, which keeps only the credential that request shows and removes the rest (see
/// <see cref="UserCredentialsProof"/>).
/// </para>
/// <para>
/// The marker is saved through the session rather than the user manager: Orchard Core's user store builds these
/// handlers, so a handler that needed the user manager would need the store that builds it. The user was just saved by
/// the confirmation, and the request's session saves this change with it.
/// </para>
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
            !UserEmailOwnership.IsVerified(user.Properties, user.Email) &&
            !UserCredentialsProof.HasCredentials(user))
        {
            UserEmailOwnership.MarkVerified(user.Properties, user.Email, _clock.UtcNow);
            await _session.SaveAsync(user);
        }
    }
}
