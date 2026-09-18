using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Core.Hosting;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Core.Hosting;

/// <summary>
/// Pins what writing to the signed-in user does when there is no signed-in user.
/// </summary>
/// <remarks>
/// Both of these are refusals rather than silent no-ops. A profile write that quietly does nothing is
/// how a soft phone ends up holding credentials the server believes it revoked.
/// </remarks>
public sealed class OrchardCoreUserProfileStoreTests
{
    [Fact]
    public async Task UpdateAsync_WhenMutateIsNull_Throws()
    {
        // Arrange
        var store = new OrchardCoreUserProfileStore(
            CreateUserManager().Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<OrchardCoreUserProfileStore>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpdateAsync<TelephonyUserConnections>(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_WhenNobodyIsSignedIn_Throws()
    {
        // Arrange
        var store = new OrchardCoreUserProfileStore(
            CreateUserManager().Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<OrchardCoreUserProfileStore>.Instance);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => store.UpdateAsync<TelephonyUserConnections>(_ => true, TestContext.Current.CancellationToken));
    }

    private static Mock<UserManager<IUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<IUser>>();

        return new Mock<UserManager<IUser>>(
            store.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
