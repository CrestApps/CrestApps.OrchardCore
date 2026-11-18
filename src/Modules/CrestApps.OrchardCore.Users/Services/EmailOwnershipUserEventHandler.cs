using System.Security.Claims;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Notices, as Orchard Core's user store saves an account, a password or an external login added, changed or removed
/// from a session an e-mailed code began, while the address isn't proved
/// (<see cref="UserCredentialsProof.ChangedInCodeSessionPropertyName"/>): the owner linked their own login, or removed
/// one, next to credentials somebody else may hold, and Orchard Core then signs the browser in again without the code's
/// mark. Every session of that account is told on every page from then on (<see cref="Filters.EmailOwnershipNoticeFilter"/>).
/// It reads only the request's own user and the account being saved.
/// </summary>
public sealed class EmailOwnershipUserEventHandler : UserEventHandlerBase
{

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    public EmailOwnershipUserEventHandler(IHttpContextAccessor httpContextAccessor, IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }


    /// <inheritdoc/>
    public override Task UpdatingAsync(UserUpdateContext context)
    {
        if (context?.User is User account &&
            !UserCredentialsProof.ChangedInCodeSession(account) &&
            UserCredentialsProof.HasCredentialsInDoubt(account) &&
            _httpContextAccessor.HttpContext?.User is { } principal &&
            UserCredentialsProof.SignedInWithEmailedCode(principal) &&
            string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), account.UserId, StringComparison.Ordinal) &&
            CredentialsChanged(principal, account))
        {
            UserCredentialsProof.MarkChangedInCodeSession(account, _clock.UtcNow);
        }

        return Task.CompletedTask;
    }

    // What the code session saw at sign-in, against the account as it is saved now. A session without it (signed in
    // before the upgrade, or refreshed by RefreshSignInAsync, which keeps only how the session began) counts any change
    // of the account as one.
    private static bool CredentialsChanged(ClaimsPrincipal principal, User account)
        => principal.FindFirstValue(UserCredentialsProof.CodeSessionCredentialsClaim) is not { Length: > 0 } atSignIn ||
            !string.Equals(atSignIn, UserCredentialsProof.CredentialsFingerprint(account), StringComparison.Ordinal);
}
