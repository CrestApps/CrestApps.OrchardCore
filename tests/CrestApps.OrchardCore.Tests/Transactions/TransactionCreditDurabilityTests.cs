using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The ledger entry is the durable intent and the credit change its idempotent follow-up: the entry is saved first,
/// waiting, the credit changes after it keyed by the entry's id, and the entry is marked settled. Against a real store,
/// so the session commit the credit change makes, while the transaction is still to be saved, is the real one.
/// </summary>
public sealed class TransactionCreditDurabilityTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PartyRef _account = new(PartyKinds.Account, "account-1");

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-credit-durability-{Guid.NewGuid():N}.db");
    private readonly List<PaymentIssue> _issues = [];
    private IStore _store;
    private ServiceProvider _provider;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new TransactionIndexProvider()], TransactionsConstants.CollectionName);
        _store.RegisterIndexes([new StoreCreditAccountIndexProvider()], CustomersConstants.CollectionName);
        _store.RegisterIndexes(new BalanceLedgerEntryIndexProvider());

        var cancellationToken = TestContext.Current.CancellationToken;

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(TransactionsConstants.CollectionName, cancellationToken);
        await _store.InitializeCollectionAsync(CustomersConstants.CollectionName, cancellationToken);

        await using (var connection = _store.Configuration.ConnectionFactory.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var schema = new SchemaBuilder(_store.Configuration, transaction);

            await new TransactionMigrations { SchemaBuilder = schema }.CreateAsync();

            var credit = new StoreCreditMigrations { SchemaBuilder = schema };
            await credit.CreateAsync();
            await credit.UpdateFrom1Async();

            var tenders = new CheckoutTenderMigrations { SchemaBuilder = schema };
            await tenders.CreateAsync();
            await tenders.UpdateFrom1Async();

            await transaction.CommitAsync(cancellationToken);
        }

        var services = new ServiceCollection();
        var clock = new TestClock(_now);

        services.AddSingleton(_store);
        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));
        services.AddScoped<ITransactionStore, TransactionStore>();
        services.AddScoped<ITransactionManager>(sp => new TransactionManager(sp.GetRequiredService<ITransactionStore>(), [], sp, NullLogger<CrestApps.Core.Services.CatalogManager<Transaction>>.Instance));
        services.AddScoped<IBalanceLedgerArchive, BalanceLedgerArchive>();
        services.AddScoped<IStoreCreditStore, StoreCreditStore>();
        services.AddScoped(sp => new StoreCreditService(sp.GetRequiredService<IStoreCreditStore>(), sp.GetRequiredService<ISession>(), sp.GetRequiredService<IDistributedLock>(), clock, sp.GetRequiredService<IBalanceLedgerArchive>()));
        services.AddScoped<ICustomerCreditService>(sp => new StoreCreditCustomerCreditService(sp.GetRequiredService<StoreCreditService>(), sp.GetRequiredService<IStoreCreditStore>(), Mock.Of<ICustomerStore>(), Mock.Of<ICustomerService>(), sp));

        var recorder = new Mock<IPaymentIssueRecorder>();
        recorder
            .Setup(candidate => candidate.RecordAsync(It.IsAny<PaymentIssue>(), It.IsAny<CancellationToken>()))
            .Callback((PaymentIssue issue, CancellationToken _) => _issues.Add(issue))
            .ReturnsAsync(true);
        services.AddSingleton(recorder.Object);

        services.AddScoped<ITransactionCommandService>(sp => new DefaultTransactionCommandService(
            sp.GetRequiredService<ITransactionManager>(),
            sp.GetRequiredService<IDistributedLock>(),
            sp.GetRequiredService<ISession>(),
            new HttpContextAccessor(),
            Mock.Of<IUserService>(),
            Mock.Of<IDisplayNameProvider>(),
            new TransactionDisplayText([], Options.Create(new TransactionSourceOptions()), new PassThroughStringLocalizer<TransactionDisplayText>(), new SourceTextLocalizerFactory()),
            sp,
            clock,
            NullLogger<DefaultTransactionCommandService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionCommandService>()));

        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);
    }

    [Fact]
    public async Task ExcessKeptAsCredit_SavesTheTransactionOnce_WithoutAConcurrencyFailure()
    {
        // Arrange
        await SaveAsync(CreateOutstanding());

        // Act: the credit service commits the session while the transaction loaded before it is still to be saved.
        var result = await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>()
            .RecordPaymentAsync("tx-1", new TransactionPaymentRequest { Amount = 160m, CreditExcess = true }, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Empty(result.Warnings);
        Assert.Equal(60m, await BalanceAsync());
        Assert.Equal(1, await CountTransactionDocumentsAsync("tx-1"));

        var stored = await FindAsync("tx-1");
        var payment = Assert.Single(stored.Events, entry => entry.Type == TransactionEventType.PaymentRecorded);
        Assert.Equal((60m, TransactionCreditState.Settled), (payment.CreditChange, payment.CreditState));
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Empty(await PendingAsync());
    }

    /// <summary>
    /// A process stopped after the credit was added but before the entry was marked: completing it again finds the change
    /// by the entry's id and moves the credit once.
    /// </summary>
    [Fact]
    public async Task AStopBetweenTheSteps_FollowedByARetry_MovesTheCreditOnce()
    {
        // Arrange: the payment entry was saved waiting, and the credit was added, but the entry was never marked.
        var transaction = CreateOutstanding();
        transaction.AmountPaid = 100m;
        transaction.Status = TransactionStatus.Paid;
        transaction.Events.Add(new TransactionEvent { Id = "pay-1", CreatedUtc = _now, Type = TransactionEventType.PaymentRecorded, Amount = 100m, Method = TransactionsConstants.SettlementMethods.Offline, CreditedAmount = 25m, CreditChange = 25m, CreditState = TransactionCreditState.Pending });
        await SaveAsync(transaction);

        await InScopeAsync(services => services.GetRequiredService<ICustomerCreditService>().AddAsync(new CustomerCreditChange
        {
            Party = _account,
            Currency = "USD",
            Amount = 25m,
            Reference = TransactionCreditSettlement.GetReference("tx-1", "pay-1"),
        }, TestContext.Current.CancellationToken));

        Assert.Equal(["tx-1"], await PendingAsync());

        // Act: the sweep completes it, and a later command finds nothing left to do.
        var reconciled = await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>().ReconcileCreditAsync("tx-1", TestContext.Current.CancellationToken));
        var again = await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>().ReconcileCreditAsync("tx-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(reconciled.Warnings);
        Assert.Empty(again.Warnings);
        Assert.Equal(25m, await BalanceAsync());
        Assert.Equal(TransactionCreditState.Settled, (await FindAsync("tx-1")).Events.Single(entry => entry.Id == "pay-1").CreditState);
        Assert.Empty(await PendingAsync());
        Assert.Equal(1, await CountTransactionDocumentsAsync("tx-1"));
    }

    [Fact]
    public async Task AWaitingEntry_IsCompletedByTheNextCommandOnTheTransaction()
    {
        // Arrange: the process stopped right after saving the entry.
        var transaction = CreateOutstanding();
        transaction.AmountPaid = 40m;
        transaction.Status = TransactionStatus.PartiallyPaid;
        transaction.Events.Add(new TransactionEvent { Id = "pay-1", CreatedUtc = _now, Type = TransactionEventType.PaymentRecorded, Amount = 40m, Method = TransactionsConstants.SettlementMethods.Offline, CreditedAmount = 15m, CreditChange = 15m, CreditState = TransactionCreditState.Pending });
        await SaveAsync(transaction);

        // Act: any command completes it first.
        var adjusted = await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>()
            .AdjustAsync("tx-1", 10m, TransactionAdjustmentKind.Discount, "Loyal customer", TestContext.Current.CancellationToken));

        // Assert
        Assert.True(adjusted.Succeeded, string.Join(" ", adjusted.Errors));
        Assert.Equal(15m, await BalanceAsync());

        var stored = await FindAsync("tx-1");
        Assert.Equal(TransactionCreditState.Settled, stored.Events.Single(entry => entry.Id == "pay-1").CreditState);
        Assert.Equal(50m, stored.OutstandingAmount);
        Assert.Equal(1, await CountTransactionDocumentsAsync("tx-1"));
    }

    [Fact]
    public async Task ASpendWithTooLittleCredit_TakesThePaymentBack()
    {
        // Arrange: a payment with credit saved waiting, but the credit was spent elsewhere meanwhile.
        var transaction = CreateOutstanding();
        transaction.AmountPaid = 40m;
        transaction.Status = TransactionStatus.PartiallyPaid;
        transaction.Events.Add(new TransactionEvent { Id = "pay-1", CreatedUtc = _now, Type = TransactionEventType.PaymentRecorded, Amount = 40m, Method = TransactionsConstants.SettlementMethods.CustomerCredit, CreditChange = -40m, CreditState = TransactionCreditState.Pending });
        await SaveAsync(transaction);

        // Act
        var reconciled = await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>().ReconcileCreditAsync("tx-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.NotEmpty(reconciled.Warnings);

        var stored = await FindAsync("tx-1");
        Assert.Equal(TransactionCreditState.Failed, stored.Events.Single(entry => entry.Id == "pay-1").CreditState);
        Assert.Contains(stored.Events, entry => entry.Type == TransactionEventType.Reversed && entry.ReversesEventId == "pay-1");
        Assert.Equal((0m, TransactionStatus.Outstanding), (stored.AmountPaid, stored.Status));
        Assert.Empty(_issues);
    }

    [Fact]
    public async Task AFailedCompletion_RecordsAPaymentIssue()
    {
        // Arrange: credit to keep for a payer that can't hold it.
        var transaction = CreateOutstanding();
        transaction.SetPayer(new PartyRef(PartyKinds.Guest, "guest-1"));
        transaction.AmountPaid = 100m;
        transaction.Status = TransactionStatus.Paid;
        transaction.Events.Add(new TransactionEvent { Id = "pay-1", CreatedUtc = _now, Type = TransactionEventType.PaymentRecorded, Amount = 100m, Method = TransactionsConstants.SettlementMethods.Offline, CreditedAmount = 25m, CreditChange = 25m, CreditState = TransactionCreditState.Pending });
        await SaveAsync(transaction);

        // Act
        await InScopeAsync(services => services.GetRequiredService<ITransactionCommandService>().ReconcileCreditAsync("tx-1", TestContext.Current.CancellationToken));

        // Assert
        var issue = Assert.Single(_issues);
        Assert.Equal((PaymentIssueKinds.CreditNotReconciled, "tx-1", 25m), (issue.Kind, issue.ReferenceId, issue.Amount));
        Assert.Equal(TransactionCreditState.Failed, (await FindAsync("tx-1")).Events.Single(entry => entry.Id == "pay-1").CreditState);
        Assert.Empty(await PendingAsync());
    }

    private static Transaction CreateOutstanding()
    {
        var transaction = new Transaction
        {
            ItemId = "tx-1",
            OwnerId = "user-1",
            OwnerKind = CrestApps.OrchardCore.Customers.Models.CustomerOwnerKind.Authenticated,
            Title = "Invoice INV-1001",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

        transaction.SetPayer(_account);

        return transaction;
    }

    private Task<Transaction> SaveAsync(Transaction transaction)
        => InScopeAsync(async services =>
        {
            await services.GetRequiredService<ITransactionStore>().CreateAsync(transaction, TestContext.Current.CancellationToken);

            return transaction;
        });

    private Task<Transaction> FindAsync(string id)
        => InScopeAsync(services => services.GetRequiredService<ITransactionStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private Task<decimal> BalanceAsync()
        => InScopeAsync(services => services.GetRequiredService<ICustomerCreditService>().GetBalanceAsync(_account, "USD", TestContext.Current.CancellationToken));

    private Task<int> CountTransactionDocumentsAsync(string id)
        => InScopeAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<TransactionIndex>(index => index.ItemId == id, collection: TransactionsConstants.CollectionName)
            .CountAsync(TestContext.Current.CancellationToken));

    private Task<IReadOnlyList<string>> PendingAsync()
        => InScopeAsync(services => TransactionCreditSettlement.GetPendingAsync(services.GetRequiredService<ISession>(), 10, TestContext.Current.CancellationToken));

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();

        var result = await work(scope.ServiceProvider);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }
}
