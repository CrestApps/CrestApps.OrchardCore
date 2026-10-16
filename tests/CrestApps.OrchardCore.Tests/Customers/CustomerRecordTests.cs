using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Everyone who buys gets one customer record: a user's is made on their first order and found again after that,
/// and statements address them by it.
/// </summary>
public sealed class CustomerRecordTests
{
    [Theory]
    [InlineData(CustomerKind.Person, "Sam Doe", null, "Pat", "Lee", "pat@example.test", "Sam Doe")]
    [InlineData(CustomerKind.Person, null, null, "Pat", "Lee", "pat@example.test", "Pat Lee")]
    [InlineData(CustomerKind.Business, null, "Contoso", "Pat", "Lee", "pat@example.test", "Contoso")]
    [InlineData(CustomerKind.Person, null, null, null, null, "pat@example.test", "pat@example.test")]
    public void AName_IsTheDisplayName_ThenTheCompanyOfABusiness_ThenThePerson_ThenTheEmail(
        CustomerKind kind, string displayName, string company, string first, string last, string email, string expected)
    {
        var customer = new Customer { Kind = kind, DisplayName = displayName, CompanyName = company, FirstName = first, LastName = last, Email = email };

        Assert.Equal(expected, customer.GetName());
    }

    [Fact]
    public async Task AUsersCustomer_IsMadeOnce_AndFoundAfterThat()
    {
        var store = new InMemoryCustomerStore();
        var service = CreateService(store, new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test" });

        var first = await service.GetOrCreateForUserAsync("user-1", TestContext.Current.CancellationToken);
        var again = await service.GetOrCreateForUserAsync("user-1", TestContext.Current.CancellationToken);

        Assert.Same(first, again);
        Assert.Single(store.Customers);
        Assert.Equal("pat", first.DisplayName);
        Assert.Equal("pat@example.test", first.Email);
        Assert.Equal("user-1", first.UserId);
    }

    [Fact]
    public async Task AUserWhoDoesNotExist_GetsNoCustomer()
    {
        var store = new InMemoryCustomerStore();
        var service = CreateService(store, user: null);

        Assert.Null(await service.GetOrCreateForUserAsync("missing", TestContext.Current.CancellationToken));
        Assert.Empty(store.Customers);
    }

    [Fact]
    public async Task AStatement_AddressesAUserByTheirCustomerRecord_AndTheAccountsEmail()
    {
        var store = new InMemoryCustomerStore();
        await store.CreateAsync(new Customer { ItemId = "c1", UserId = "user-1", DisplayName = "Pat Lee", Email = "old@example.test" }, TestContext.Current.CancellationToken);

        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserByUniqueIdAsync("user-1")).ReturnsAsync(new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test" });

        var resolver = new CustomerRecordContactResolver(store, new DefaultCustomerContactResolver(users.Object));
        var contact = await resolver.ResolveAsync(CustomerOwner.ForUser("user-1"), null, TestContext.Current.CancellationToken);

        Assert.Equal("Pat Lee", contact.DisplayName);
        Assert.Equal("pat@example.test", contact.Email);

        // A guest is still addressed from what was kept at purchase.
        var guest = new CustomerContact { DisplayName = "Guest", Email = "guest@example.test" };
        Assert.Same(guest, await resolver.ResolveAsync(CustomerOwner.ForGuest("g1"), guest, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheList_FindsCustomersByTheStartOfTheirNameCompanyOrEmail_AndFilters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-customers-{Guid.NewGuid():N}.db");
        var yesSql = await CreateStoreAsync(path);

        try
        {
            await using (var session = yesSql.CreateSession())
            {
                var store = new CustomerStore(session);
                var now = DateTime.UtcNow;

                await store.CreateAsync(new Customer { ItemId = "c1", DisplayName = "Pat Lee", Email = "pat@example.test", UserId = "user-1", LastActivityUtc = now }, TestContext.Current.CancellationToken);
                await store.CreateAsync(new Customer { ItemId = "c2", Kind = CustomerKind.Business, CompanyName = "Contoso", Email = "buyer@contoso.test", LastActivityUtc = now.AddDays(-1) }, TestContext.Current.CancellationToken);
                await store.CreateAsync(new Customer { ItemId = "c3", DisplayName = "Old Guest", Email = "old@example.test", Status = CustomerStatus.Disabled, LastActivityUtc = now.AddDays(-2) }, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var store = new CustomerStore(session);

                Assert.Equal(["c1"], await IdsAsync(store, new CustomerQuery { Search = "pat" }));
                Assert.Equal(["c2"], await IdsAsync(store, new CustomerQuery { Search = "CONTO" }));
                Assert.Equal(["c2"], await IdsAsync(store, new CustomerQuery { Search = "buyer@" }));
                Assert.Equal(["c1"], await IdsAsync(store, new CustomerQuery { HasAccount = true }));
                Assert.Equal(["c2"], await IdsAsync(store, new CustomerQuery { Kind = CustomerKind.Business }));
                Assert.Equal(["c1", "c2"], await IdsAsync(store, new CustomerQuery { Status = CustomerStatus.Active }));

                // The most recently active come first.
                Assert.Equal(["c1", "c2", "c3"], await IdsAsync(store, new CustomerQuery()));
                Assert.Equal("c1", (await store.FindByUserIdAsync("user-1", TestContext.Current.CancellationToken)).ItemId);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }

    private static async Task<string[]> IdsAsync(CustomerStore store, CustomerQuery query)
        => (await store.PageAsync(query, 0, 50, TestContext.Current.CancellationToken)).Customers.Select(customer => customer.ItemId).ToArray();

    private static DefaultCustomerService CreateService(ICustomerStore store, User user)
    {
        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserByUniqueIdAsync(It.IsAny<string>())).ReturnsAsync((IUser)user);

        var locker = new Mock<ILocker>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(locking => locking.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker.Object, true));

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));

        return new DefaultCustomerService(store, users.Object, distributedLock.Object, clock.Object, NullLogger<DefaultCustomerService>.Instance);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CustomerIndexProvider()], CustomersConstants.CollectionName);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(CustomersConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new CustomerMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private sealed class InMemoryCustomerStore : ICustomerStore
    {
        public List<Customer> Customers { get; } = [];

        public ValueTask<Customer> FindByIdAsync(string customerId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Customers.FirstOrDefault(customer => customer.ItemId == customerId));

        public Task<Customer> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Customers.FirstOrDefault(customer => customer.UserId == userId));

        public Task<(int Count, IReadOnlyList<Customer> Customers)> PageAsync(CustomerQuery query, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Customer>)>((Customers.Count, Customers));

        public ValueTask CreateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            Customers.Add(customer);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
