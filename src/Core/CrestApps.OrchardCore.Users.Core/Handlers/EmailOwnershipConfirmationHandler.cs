using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Records the address as proved (<see cref="UserEmailOwnership"/>) when a user follows the link e-mailed to confirm
/// it: the link is a round trip to the mailbox. With <see cref="Services.EmailBoundConfirmationTokenProvider"/> in
/// place, a link only confirms the address it was sent to.
/// </summary>
/// <remarks>
/// <para>
/// The link proves who reads the mail, not who set the account's password or linked its external logins: someone can
/// register with another person's address and a password of their own, and that person may follow the "confirm your
/// account" mail. So an account with credentials is marked only when the same request shows them (signed in to it with
/// its password or one of its own external logins, see <see cref="UserCredentialsProof"/>). Otherwise Orchard Core
/// still confirms the address, but nothing is marked, so nothing that belongs to the address is handed over; the owner
/// confirms on the "Confirm it's you" page, with the password or by taking the account back.
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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOwnershipConfirmationHandler"/> class.
    /// </summary>
    /// <param name="session">The request's session.</param>
    /// <param name="httpContextAccessor">The request, to tell whether it shows the account's credentials.</param>
    /// <param name="clock">The clock.</param>
    public EmailOwnershipConfirmationHandler(ISession session, IHttpContextAccessor httpContextAccessor, IClock clock)
    {
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override async Task ConfirmedAsync(UserConfirmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ConfirmationType == UserConfirmationType.Email &&
            context.User is User user &&
            !UserEmailOwnership.IsVerified(user.Properties, user.Email) &&
            (!UserCredentialsProof.HasCredentials(user) || UserCredentialsProof.ShowsCredentials(_httpContextAccessor.HttpContext?.User, user)))
        {
            UserEmailOwnership.MarkVerified(user.Properties, user.Email, _clock.UtcNow);
            await _session.SaveAsync(user);
        }
    }
}
