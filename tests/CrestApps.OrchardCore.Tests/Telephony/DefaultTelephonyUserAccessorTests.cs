using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyUserAccessorTests
{
    [Fact]
    public async Task UpdateUserAsync_WhenIdentityResultFails_Throws()
    {
        // Arrange
        var user = new FakeUser();
        var userManager = CreateUserManager();
        userManager
            .Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure", Description = "Optimistic concurrency failure." }));

        var accessor = new DefaultTelephonyUserAccessor(
            userManager.Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<DefaultTelephonyUserAccessor>.Instance);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => accessor.UpdateUserAsync(user));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenIdentityResultSucceeds_DoesNotThrow()
    {
        // Arrange
        var user = new FakeUser();
        var userManager = CreateUserManager();
        userManager
            .Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var accessor = new DefaultTelephonyUserAccessor(
            userManager.Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<DefaultTelephonyUserAccessor>.Instance);

        // Act
        await accessor.UpdateUserAsync(user);

        // Assert
        userManager.Verify(manager => manager.UpdateAsync(user), Times.Once);
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
