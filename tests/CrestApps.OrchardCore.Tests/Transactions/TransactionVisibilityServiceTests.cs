using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Parties;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A signed-in user sees the transactions they own and those owed by a party they have billing access to. Of an account,
/// only its admins do: a plain member (a buyer) sees only what they own, also when it is billed to the account.
/// </summary>
public sealed class TransactionVisibilityServiceTests
{
    private const string AccountId = "acct-1";
    private const string Admin = "admin-1";
    private const string Buyer = "buyer-1";

    private static readonly PartyRef _account = new(PartyKinds.Account, AccountId);

    [Fact]
    public async Task CanView_TheirOwnTransaction()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.True(await service.CanViewAsync(new Transaction { OwnerId = "u1", OwnerKind = CustomerOwnerKind.Authenticated }, "u1", TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(new Transaction { OwnerId = "u1", OwnerKind = CustomerOwnerKind.Guest }, "u1", TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(new Transaction { OwnerId = "u2", OwnerKind = CustomerOwnerKind.Authenticated }, "u1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanView_TheAccountsTransaction_OnlyAsAnAdminOfTheAccount()
    {
        // Arrange
        var service = CreateService();
        var owedByAccount = OwedByAccount(owner: "someone-else");

        // Act & Assert
        Assert.True(await service.CanViewAsync(owedByAccount, Admin, TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(owedByAccount, Buyer, TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(owedByAccount, "stranger", TestContext.Current.CancellationToken));
        Assert.False(await service.CanViewAsync(owedByAccount, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanView_ABuyersOwnTransactionBilledToTheAccount()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.True(await service.CanViewAsync(OwedByAccount(owner: Buyer), Buyer, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateQuery_ForAnAdmin_ListsTheirOwnAndTheAccountsTransactions()
    {
        // Arrange
        var service = CreateService();

        // Act
        var query = await service.CreateQueryAsync(Admin, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Admin, query.OwnerId);
        Assert.Equal(CustomerOwnerKind.Authenticated, query.OwnerKind);
        Assert.Contains(_account, query.Payers);
        Assert.Contains(new PartyRef(PartyKinds.User, Admin), query.Payers);
    }

    [Fact]
    public async Task CreateQuery_ForAPlainMember_CarriesNoAccountPayer()
    {
        // Arrange
        var service = CreateService();

        // Act
        var query = await service.CreateQueryAsync(Buyer, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Buyer, query.OwnerId);
        Assert.DoesNotContain(_account, query.Payers);
        Assert.Equal(new[] { new PartyRef(PartyKinds.User, Buyer) }, query.Payers);
    }

    internal static Transaction OwedByAccount(string owner)
        => new()
        {
            OwnerId = owner,
            OwnerKind = CustomerOwnerKind.Authenticated,
            PayerKind = PartyKinds.Account,
            PayerId = AccountId,
        };

    private static DefaultTransactionVisibilityService CreateService()
        => new(AccountMembershipFixture.CreateMembership(AccountId, (Admin, AccountMemberRole.Admin), (Buyer, AccountMemberRole.Member)));
}
