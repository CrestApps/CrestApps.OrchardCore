using System.Security.Claims;
using CrestApps.OrchardCore.Users.Core.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Marks the sign-in of an account whose address isn't proved, that has a password or an external login, and whose
/// credentials changed in a session an e-mailed code began (<see cref="UserCredentialsProof.CredentialsInDoubtClaim"/>,
/// <see cref="UserCredentialsProof.ChangedInCodeSessionPropertyName"/>), so <see cref="Filters.EmailOwnershipNoticeFilter"/>
/// reads the account for that session only. Any other account with credentials in doubt is told at sign-in only
/// (<see cref="EmailOwnershipLoginFormEvent"/>). It reads nothing itself: Orchard Core's claims principal factory hands it
/// the account it is building the session's claims from, at every sign-in, <c>RefreshSignInAsync</c> and Identity's
/// stamp check.
/// </summary>
public sealed class EmailOwnershipClaimsProvider : IUserClaimsProvider
{
    /// <inheritdoc/>
    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        if (user is User account && UserCredentialsProof.HasCredentialsInDoubt(account) && UserCredentialsProof.ChangedInCodeSession(account))
        {
            claims.AddClaim(new Claim(UserCredentialsProof.CredentialsInDoubtClaim, "true"));
        }

        return Task.CompletedTask;
    }
}
