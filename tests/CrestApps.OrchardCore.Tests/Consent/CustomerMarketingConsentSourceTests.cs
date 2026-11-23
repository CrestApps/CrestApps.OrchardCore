using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// A customer's consent to marketing on a real SQLite store: an address-only "stop all marketing" reaches every
/// customer record that holds the address, whatever its case, and a question that names a customer but no id is
/// answered by the address. A question that names no customer is not this source's to answer.
/// </summary>
public sealed class CustomerMarketingConsentSourceTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"customer-consent-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CustomerIndexProvider()], CustomersConstants.CollectionName);
        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CustomersConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            await new CustomerMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var customers = new CustomerStore(session, []);

            // A guest record and an account record of one person, an erased record, and someone else.
            await customers.CreateAsync(new Customer { ItemId = "guest", Email = "Sam@Example.com", AcceptsMarketing = true }, cancellationToken);
            await customers.CreateAsync(new Customer { ItemId = "account", UserId = "user-1", Email = "sam@example.com", AcceptsMarketing = true }, cancellationToken);
            await customers.CreateAsync(new Customer { ItemId = "merged", Email = "sam@example.com", AcceptsMarketing = true, Status = CustomerStatus.Merged }, cancellationToken);
            await customers.CreateAsync(new Customer { ItemId = "kim", Email = "kim@example.com", AcceptsMarketing = true }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RecordAsync_AnAddressOnlyMarketingOptOut_ReachesEveryCustomerAtTheAddress()
    {
        // Act
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, Source = "unsubscribe-link" }, "SAM@example.com ");

        // Assert
        Assert.False(await AcceptsMarketingAsync("guest"));
        Assert.False(await AcceptsMarketingAsync("account"));
        Assert.True(await AcceptsMarketingAsync("merged"));
        Assert.True(await AcceptsMarketingAsync("kim"));
    }

    [Fact]
    public async Task RecordAsync_AnOptInByAddress_SetsAcceptsMarketingAgain()
    {
        // Arrange
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing }, "sam@example.com");

        // Act
        await RecordAsync(new ConsentChange { OptedOut = false, Purpose = ConsentPurposes.Marketing }, "sam@example.com");

        // Assert
        Assert.True(await AcceptsMarketingAsync("guest"));
        Assert.True(await AcceptsMarketingAsync("account"));
    }

    [Fact]
    public async Task RecordAsync_AChangeThatNamesACustomer_ChangesOnlyThatCustomer()
    {
        // Act
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, ContactKind = ConsentContactKinds.Customer, ContactId = "account" }, "sam@example.com");

        // Assert
        Assert.False(await AcceptsMarketingAsync("account"));
        Assert.True(await AcceptsMarketingAsync("guest"));
    }

    [Fact]
    public async Task RecordAsync_AChangeForACrmContact_FindsTheCustomersByAddress()
    {
        // Act
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, ContactKind = ConsentContactKinds.Contact, ContactId = "contact-1" }, "sam@example.com");

        // Assert
        Assert.False(await AcceptsMarketingAsync("guest"));
        Assert.False(await AcceptsMarketingAsync("account"));
    }

    [Fact]
    public async Task RecordAsync_AnOptOutOfEveryPurposeThatNamesNoCustomer_IsNotThisSourcesToKeep()
    {
        // Act
        await RecordAsync(new ConsentChange { OptedOut = true, Source = "Complaint" }, "sam@example.com");

        // Assert
        Assert.True(await AcceptsMarketingAsync("guest"));
        Assert.True(await AcceptsMarketingAsync("account"));
    }

    [Fact]
    public async Task HasOptedOutAsync_ACustomerQuestionWithoutAnId_IsAnsweredByTheAddress()
    {
        // Arrange
        var question = new ConsentQuery { Channel = ConsentChannels.Email, Address = "Sam@Example.com", ContactKind = ConsentContactKinds.Customer, Purpose = ConsentPurposes.Marketing };
        var before = await HasOptedOutAsync(question);
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing }, "sam@example.com");

        // Act
        var after = await HasOptedOutAsync(question);

        // Assert
        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public async Task HasOptedOutAsync_AQuestionThatNamesNoCustomer_IsNotAnswered()
    {
        // Arrange
        await RecordAsync(new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing }, "sam@example.com");

        // Act
        var marketing = await HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing });

        // Assert
        Assert.False(marketing);
    }

    private async Task RecordAsync(ConsentChange change, string address)
    {
        await using var session = _store.CreateSession();
        await Source(session).RecordAsync(ConsentChannels.Email, address, change, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> HasOptedOutAsync(ConsentQuery query)
    {
        await using var session = _store.CreateSession();

        return await Source(session).HasOptedOutAsync(query, TestContext.Current.CancellationToken);
    }

    private async Task<bool> AcceptsMarketingAsync(string customerId)
    {
        await using var session = _store.CreateSession();

        return (await new CustomerStore(session, []).FindByIdAsync(customerId, TestContext.Current.CancellationToken)).AcceptsMarketing;
    }

    private static CustomerMarketingConsentSource Source(ISession session)
        => new(new CustomerStore(session, []), session, new TestClock(_now));
}
