using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Accounts;

/// <summary>
/// The membership an account page and its menu link both read: the signed-in user's account and member entry, an admin
/// only when the entry says so, and none for a guest or for a user the account doesn't list.
/// </summary>
public sealed class AccountMembershipTests
{
    [Fact]
    public async Task FindMembershipAsync_ReturnsTheAccountAndTheMember_WithTheAdminRole()
    {
        // Arrange
        var service = Service(Account(("u1", AccountMemberRole.Admin), ("u2", AccountMemberRole.Member)));

        // Act
        var admin = await service.FindMembershipAsync(User("u1"), TestContext.Current.CancellationToken);
        var member = await service.FindMembershipAsync(User("u2"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(admin.IsAdmin);
        Assert.Equal("u1", admin.Member.UserId);
        Assert.False(member.IsAdmin);
        Assert.Equal("acme", member.Account.ContentItemId);
    }

    [Fact]
    public async Task FindMembershipAsync_ReturnsNone_ForAGuest_AUserInNoAccount_OrAUserTheAccountDoesNotList()
    {
        // Arrange
        var inAccount = Service(Account(("u1", AccountMemberRole.Admin)));
        var noAccount = Service(null);

        // Act and assert
        Assert.Null(await inAccount.FindMembershipAsync(new ClaimsPrincipal(new ClaimsIdentity()), TestContext.Current.CancellationToken));
        Assert.Null(await inAccount.FindMembershipAsync(null, TestContext.Current.CancellationToken));
        Assert.Null(await inAccount.FindMembershipAsync(User("u9"), TestContext.Current.CancellationToken));
        Assert.Null(await noAccount.FindMembershipAsync(User("u1"), TestContext.Current.CancellationToken));
    }

    private static IAccountMemberService Service(ContentItem account)
    {
        var service = new Mock<IAccountMemberService>();

        service
            .Setup(members => members.FindAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        return service.Object;
    }

    private static ContentItem Account(params (string UserId, AccountMemberRole Role)[] members)
    {
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account" };

        account.Apply(new AccountMembersPart
        {
            Members = [.. members.Select(member => new AccountMember { UserId = member.UserId, UserName = member.UserId, Role = member.Role })],
        });

        return account;
    }

    private static ClaimsPrincipal User(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
}
