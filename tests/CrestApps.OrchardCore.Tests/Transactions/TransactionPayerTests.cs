using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A transaction names the party that owes it, which is not always the person who may pay it: an account owes its
/// invoices and each member may pay them. These cover the payer itself, what a part payment may be, and the query that
/// lists a person's own debts with those of the parties they act for, against a real store.
/// </summary>
public sealed class TransactionPayerTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-payer-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new TransactionIndexProvider()], TransactionsConstants.CollectionName);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(TransactionsConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await new TransactionMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public void GetPayer_IsThePayerRecorded()
    {
        // Arrange
        var transaction = new Transaction { OwnerId = "user-1" };

        transaction.SetPayer(new PartyRef(PartyKinds.Customer, "customer-9"));

        // Act
        var payer = transaction.GetPayer();

        // Assert
        Assert.Equal(new PartyRef(PartyKinds.Customer, "customer-9"), payer);
    }

    [Theory]
    [InlineData(CustomerOwnerKind.Authenticated, PartyKinds.User)]
    [InlineData(CustomerOwnerKind.Guest, PartyKinds.Guest)]
    public void GetPayer_WithoutARecordedPayer_FollowsFromTheOwner(CustomerOwnerKind ownerKind, string expectedKind)
    {
        // Arrange
        var transaction = new Transaction { OwnerId = "owner-1", OwnerKind = ownerKind };

        // Act
        var payer = transaction.GetPayer();

        // Assert
        Assert.Equal(new PartyRef(expectedKind, "owner-1"), payer);
    }

    [Fact]
    public void GetPayer_OfAnAccountTermsDebt_IsTheAccountItsParentReferenceNames()
    {
        // Arrange
        var transaction = new Transaction
        {
            OwnerId = "buyer-1",
            ParentReferenceType = PartyKinds.Account,
            ParentReferenceId = "account-1",
        };

        // Act
        var payer = transaction.GetPayer();

        // Assert
        Assert.Equal(new PartyRef(PartyKinds.Account, "account-1"), payer);
    }

    [Fact]
    public void GetPayer_OfATransactionThatNamesNobody_IsEmpty()
    {
        // Act
        var payer = new Transaction().GetPayer();

        // Assert
        Assert.True(payer.IsEmpty);
    }

    [Theory]
    [InlineData(false, null, null, 100, true)]
    [InlineData(false, null, 40, 100, true)]
    [InlineData(true, null, null, 100, true)]
    [InlineData(true, null, 40, 40, true)]
    [InlineData(true, null, 250, 100, true)]
    [InlineData(true, 25, 40, 40, true)]
    [InlineData(true, 25, 10, 25, false)]
    [InlineData(true, 500, 100, 100, true)]
    [InlineData(true, 500, 60, 100, false)]
    [InlineData(true, null, 0, 100, false)]
    public void TryGetPaymentAmount_AppliesThePartPaymentRules(bool allowPartial, int? minimum, int? requested, int expectedAmount, bool expectedAccepted)
    {
        // Arrange
        var transaction = new Transaction
        {
            Currency = "USD",
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            AllowPartialPayment = allowPartial,
            MinimumPayment = minimum,
        };

        // Act
        var accepted = TransactionStateMachine.TryGetPaymentAmount(transaction, requested, out var amount);

        // Assert
        Assert.Equal(expectedAccepted, accepted);
        Assert.Equal(expectedAmount, amount);
    }

    [Fact]
    public async Task PageAsync_WithPayers_ListsTheOwnersDebtsAndThoseTheirPartiesOwe()
    {
        // Arrange
        await SaveAsync(
            Create("own", ownerId: "user-1"),
            Create("account", ownerId: "user-2", payer: new PartyRef(PartyKinds.Account, "account-1")),
            Create("legacy-account", ownerId: "user-3", parentAccountId: "account-1"),
            Create("other-account", ownerId: "user-2", payer: new PartyRef(PartyKinds.Account, "account-2")),
            Create("stranger", ownerId: "user-4"));

        await using var session = _store.CreateSession();
        var store = new TransactionStore(session, Mock.Of<IClock>());

        // Act
        var page = await store.PageAsync(1, 20, new TransactionQuery
        {
            OwnerId = "user-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Payers = [new PartyRef(PartyKinds.Account, "account-1")],
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, page.Count);
        Assert.Equal(["account", "legacy-account", "own"], page.Entries.Select(entry => entry.ItemId).Order());
    }

    [Fact]
    public async Task PageAsync_WithPayersOnly_ListsWhatThosePartiesOwe()
    {
        // Arrange
        await SaveAsync(
            Create("customer", ownerId: "user-1", payer: new PartyRef(PartyKinds.Customer, "customer-1")),
            Create("account", ownerId: "user-1", payer: new PartyRef(PartyKinds.Account, "account-1")),
            Create("own", ownerId: "user-1"));

        await using var session = _store.CreateSession();
        var store = new TransactionStore(session, Mock.Of<IClock>());

        // Act
        var page = await store.PageAsync(1, 20, new TransactionQuery
        {
            Payers = [new PartyRef(PartyKinds.Customer, "customer-1"), new PartyRef(PartyKinds.Account, "account-1")],
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["account", "customer"], page.Entries.Select(entry => entry.ItemId).Order());
    }

    private async Task SaveAsync(params Transaction[] transactions)
    {
        await using var session = _store.CreateSession();

        foreach (var transaction in transactions)
        {
            await session.SaveAsync(transaction, collection: TransactionsConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Transaction Create(string itemId, string ownerId, PartyRef payer = default, string parentAccountId = null)
    {
        var transaction = new Transaction
        {
            ItemId = itemId,
            Title = itemId,
            OwnerId = ownerId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc),
        };

        if (!payer.IsEmpty)
        {
            transaction.SetPayer(payer);
        }

        if (parentAccountId is not null)
        {
            transaction.ParentReferenceType = PartyKinds.Account;
            transaction.ParentReferenceId = parentAccountId;
        }

        return transaction;
    }
}
