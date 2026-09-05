using CrestApps.OrchardCore.Telephony.Services;
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
    public async Task PersistCurrentUserAsync_WhenMutateIsNull_Throws()
    {
        // Arrange
        var accessor = new DefaultTelephonyUserAccessor(
            CreateUserManager().Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<DefaultTelephonyUserAccessor>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => accessor.PersistCurrentUserAsync(null!));
    }

    [Fact]
    public async Task PersistCurrentUserAsync_WhenNoAuthenticatedUser_Throws()
    {
        // Arrange
        var accessor = new DefaultTelephonyUserAccessor(
            CreateUserManager().Object,
            new HttpContextAccessor(),
            new Mock<ISession>().Object,
            NullLogger<DefaultTelephonyUserAccessor>.Instance);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => accessor.PersistCurrentUserAsync(_ => true));
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
