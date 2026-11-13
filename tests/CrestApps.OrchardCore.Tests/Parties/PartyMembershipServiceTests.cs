using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Users.Core.Services;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// Who may act for a party is answered across kinds by each kind's member source: a user acts for themselves, for their
/// customer record and for the account they are a member of.
/// </summary>
public sealed class PartyMembershipServiceTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task GetPartiesAsync_ListsTheUserFirst_ThenTheirCustomerAndAccount_EachOnce()
    {
        // Arrange
        var service = CreateService();

        // Act
        var parties = await service.GetPartiesAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [new PartyRef(PartyKinds.User, UserId), new PartyRef(PartyKinds.Customer, "customer-1"), new PartyRef(PartyKinds.Account, "account-1")],
            parties);
        Assert.Empty(await service.GetPartiesAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPartiesAsync_AlwaysIncludesTheUser_WithoutAnySource()
    {
        // Arrange
        var service = new PartyMembershipService([]);

        // Act
        var parties = await service.GetPartiesAsync(UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([new PartyRef(PartyKinds.User, UserId)], parties);
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.User, UserId), UserId, TestContext.Current.CancellationToken));
        Assert.Equal([UserId], await service.GetMemberUserIdsAsync(new PartyRef(PartyKinds.User, UserId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsMemberAsync_AnswersForEachKind_FromItsSource()
    {
        // Arrange
        var service = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act and assert
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.Account, "account-1"), UserId, cancellationToken));
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.Account, "account-1"), "user-2", cancellationToken));
        Assert.False(await service.IsMemberAsync(new PartyRef(PartyKinds.Account, "account-1"), "stranger", cancellationToken));
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.Customer, "customer-1"), UserId, cancellationToken));
        Assert.False(await service.IsMemberAsync(new PartyRef(PartyKinds.Customer, "guest-1"), UserId, cancellationToken));
        Assert.False(await service.IsMemberAsync(new PartyRef(PartyKinds.User, "user-2"), UserId, cancellationToken));
        Assert.False(await service.IsMemberAsync(new PartyRef(PartyKinds.Contact, "contact-1"), UserId, cancellationToken));
        Assert.False(await service.IsMemberAsync(default, UserId, cancellationToken));
        Assert.Equal([UserId, "user-2"], await service.GetMemberUserIdsAsync(new PartyRef(PartyKinds.Account, "account-1"), cancellationToken));
        Assert.Empty(await service.GetMemberUserIdsAsync(new PartyRef(PartyKinds.Customer, "guest-1"), cancellationToken));
    }

    [Fact]
    public async Task BillingAccess_OfAnAccount_IsForItsAdminsOnly_WhilePlainMembershipCoversEveryMember()
    {
        // Arrange
        var service = AccountMembershipFixture.CreateMembership("account-1", ("admin-1", AccountMemberRole.Admin), ("buyer-1", AccountMemberRole.Member));
        var account = new PartyRef(PartyKinds.Account, "account-1");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert
        Assert.True(await service.IsMemberAsync(account, "admin-1", PartyAccess.Billing, cancellationToken));
        Assert.False(await service.IsMemberAsync(account, "buyer-1", PartyAccess.Billing, cancellationToken));
        Assert.True(await service.IsMemberAsync(account, "buyer-1", cancellationToken));
        Assert.Equal(["admin-1"], await service.GetMemberUserIdsAsync(account, PartyAccess.Billing, cancellationToken));
        Assert.Equal(["admin-1", "buyer-1"], await service.GetMemberUserIdsAsync(account, cancellationToken));
        Assert.Contains(account, await service.GetPartiesAsync("admin-1", PartyAccess.Billing, cancellationToken));
        Assert.DoesNotContain(account, await service.GetPartiesAsync("buyer-1", PartyAccess.Billing, cancellationToken));
        Assert.Contains(account, await service.GetPartiesAsync("buyer-1", cancellationToken));
    }

    [Fact]
    public async Task BillingAccess_OfTheirOwnUserAndCustomer_IsTheUsers()
    {
        // Arrange
        var service = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var parties = await service.GetPartiesAsync(UserId, PartyAccess.Billing, cancellationToken);

        // Assert
        Assert.Contains(new PartyRef(PartyKinds.User, UserId), parties);
        Assert.Contains(new PartyRef(PartyKinds.Customer, "customer-1"), parties);
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.Customer, "customer-1"), UserId, PartyAccess.Billing, cancellationToken));
        Assert.True(await service.IsMemberAsync(new PartyRef(PartyKinds.User, UserId), UserId, PartyAccess.Billing, cancellationToken));
    }

    [Fact]
    public async Task BillingAccess_FromASourceThatDoesNotGrantIt_IsNobodys()
    {
        // Arrange: a source that only knows plain members.
        var service = new PartyMembershipService([new PlainMembersSource()]);
        var club = new PartyRef("Club", "club-1");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert
        Assert.True(await service.IsMemberAsync(club, UserId, cancellationToken));
        Assert.False(await service.IsMemberAsync(club, UserId, PartyAccess.Billing, cancellationToken));
        Assert.DoesNotContain(club, await service.GetPartiesAsync(UserId, PartyAccess.Billing, cancellationToken));
    }

    private static PartyMembershipService CreateService()
    {
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer { ItemId = "customer-1", UserId = UserId });
        customers.Customers.Add(new Customer { ItemId = "guest-1" });

        var accountMembers = new Mock<IAccountMemberService>();
        accountMembers
            .Setup(service => service.GetMemberUserIdsAsync("account-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([UserId, "user-2", UserId]);
        accountMembers
            .Setup(service => service.FindAccountAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "account-1" });

        return new PartyMembershipService(
        [
            new UserPartyMemberSource(),
            new CustomerPartyMemberSource(customers),
            new AccountPartyMemberSource(accountMembers.Object),
        ]);
    }

    // Knows only plain members, so it relies on the interface's default, which grants billing access to no one.
    private sealed class PlainMembersSource : IPartyMemberSource
    {
        public string Kind => "Club";

        public Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([UserId]);

        public Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyRef>>([new PartyRef("Club", "club-1")]);
    }
}
