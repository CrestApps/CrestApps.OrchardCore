using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// Mild (third fix-verification review): Orchard Core's main branch added passkeys on 12 November 2026. The first proof
/// of an address does not remove them, <c>UserCredentialsProof.HasCredentials</c> does not count them, and a passkey
/// session carries the same <c>amr</c> as a password one, so it would read as "password shown". None of that is
/// reachable at the pinned Orchard Core build: its user store keeps no passkeys, so ASP.NET Core Identity can't sign
/// anyone in with one. This fails once the pin moves to a build whose store keeps them, so those three places are
/// updated with it.
/// </summary>
public sealed class PasskeySignInTripwireTests
{
    [Fact]
    public void OrchardCoresUserStore_KeepsNoPasskeys()
        => Assert.False(
            typeof(IUserPasskeyStore<IUser>).IsAssignableFrom(typeof(UserStore)),
            "Orchard Core's user store now keeps passkeys. Make EmailOwnershipProof remove them on the first proof of an address, " +
            "count them in UserCredentialsProof.HasCredentials, and tell a passkey session from a password one in " +
            "UserCredentialsProof.ShowsPassword before moving the pin.");
}
