using CrestApps.Core.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Moq;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users.Services;
using OrchardCore.Entities;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A real SQLite store with the invoicing and ledger services, so invoice commands run against the transaction they post
/// exactly as on a site: each command in its own scope, with its own session.
/// </summary>
internal sealed class InvoicingTestHarness : IAsyncDisposable
{
    public static readonly DateTime Now = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly string _path;
    private readonly IStore _store;
    private readonly ServiceProvider _provider;

    private InvoicingTestHarness(string path, IStore store, ServiceProvider provider)
    {
        _path = path;
        _store = store;
        _provider = provider;
    }

    public FakeNumberGenerator Numbers { get; } = new();

    public Mock<ICustomerContactResolver> Contacts { get; } = new();

    /// <summary>
    /// Stands in for Store Credit: no one can hold credit unless a test says so.
    /// </summary>
    public Mock<ICustomerCreditService> Credit { get; } = new();

    /// <summary>
    /// Renders a business document as a "PDF" of its kind, number and watermark, and counts the renderings.
    /// </summary>
    public FakePdfRenderer Pdf { get; } = new();

    /// <summary>
    /// The e-mails sent, with the content each was built with.
    /// </summary>
    public List<(CommerceEmail Email, CommerceEmailContent Content)> Emails { get; } = [];

    /// <summary>
    /// Gets or sets whether e-mail is set up.
    /// </summary>
    public bool CanSendEmail { get; set; } = true;

    /// <summary>
    /// The folder the archived PDFs are kept in.
    /// </summary>
    public string ArchiveFolder { get; } = Path.Combine(Path.GetTempPath(), $"crestapps-invoicing-archive-{Guid.NewGuid():N}");

    /// <summary>
    /// Handlers run when a transaction is created, a seam a test uses to slow down or break the step of issue that posts
    /// the transaction, after the invoice took its number.
    /// </summary>
    public List<ICatalogEntryHandler<Transaction>> TransactionHandlers { get; } = [];

    /// <summary>
    /// Gets or sets whether the next transaction created fails to save, once.
    /// </summary>
    public bool FailNextTransaction { get; set; }

    /// <summary>
    /// Runs when a transaction's status changes, while the ledger command still has it open: a seam a test uses to make a
    /// concurrent change to the transaction.
    /// </summary>
    public Func<Transaction, Task> OnTransactionStatusChanged { get; set; }

    /// <summary>
    /// The transactions whose invoice mirror was deferred until the unit of work ended; run after each one, as the site
    /// runs deferred work after a request.
    /// </summary>
    public AfterScopeSyncs Deferred { get; } = new();

    /// <summary>
    /// What the invoice observers heard, in order, written <c>Event:Number</c> (the invoice's id while it is a draft), with
    /// the amount for a payment and the credit note's number for a credit.
    /// </summary>
    public List<string> Observed { get; } = [];

    /// <summary>
    /// Numbers the other series (estimates) as <c>EST-1001</c>, <c>EST-1002</c>..., without a store.
    /// </summary>
    public FakeNumberSeries Series { get; } = new();

    /// <summary>
    /// The saved cards the payers kept, for the charges of invoices marked to be charged automatically.
    /// </summary>
    public Mock<ISavedPaymentMethodStore> SavedCards { get; } = new();

    /// <summary>
    /// The orders the order invoicing reads.
    /// </summary>
    public Mock<CrestApps.OrchardCore.Orders.Core.Services.IOrderStore> Orders { get; } = new();

    /// <summary>
    /// What each charge of a saved card comes to, in order; a charge that goes through records the payment.
    /// </summary>
    public Queue<SavedPaymentMethodChargeStatus> ChargeOutcomes { get; } = new();

    /// <summary>
    /// The saved cards charged, in order.
    /// </summary>
    public List<string> ChargedCards { get; } = [];

    /// <summary>
    /// Creates the harness. With <paramref name="realNumbers"/>, invoices are numbered by the site's generator over the
    /// number sequence kept in the store, whose series lock is <paramref name="numberLock"/> (the harness's lock when
    /// none is given); otherwise by <see cref="FakeNumberGenerator"/>.
    /// </summary>
    public static async Task<InvoicingTestHarness> CreateAsync(InvoicingSettings settings = null, bool realNumbers = false, IDistributedLock numberLock = null, bool writeAheadLog = false, string reportingCurrency = null, Action<IServiceCollection> configureServices = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-invoicing-{Guid.NewGuid():N}.db");

        // In write-ahead-log mode another connection can commit while a unit of work has the database open, as two
        // requests do on a server database.
        if (writeAheadLog)
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

        store.RegisterIndexes([new TransactionIndexProvider(), new CrestApps.OrchardCore.Transactions.Core.Indexes.PartyCollectionSettingsIndexProvider()], TransactionsConstants.CollectionName);
        store.RegisterIndexes([new CrestApps.OrchardCore.ExchangeRates.Core.Indexes.ExchangeRateIndexProvider()], CrestApps.OrchardCore.ExchangeRates.ExchangeRatesConstants.CollectionName);
        store.RegisterIndexes([new InvoiceIndexProvider(), new CreditNoteIndexProvider(), new EstimateIndexProvider(), new RecurringPlanIndexProvider(), new LateFeePolicyIndexProvider(), new BillableItemIndexProvider(), new UsageMeterIndexProvider(), new UsageRecordIndexProvider()], InvoicingConstants.CollectionName);

        var cancellationToken = TestContext.Current.CancellationToken;

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(TransactionsConstants.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(InvoicingConstants.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(CrestApps.OrchardCore.ExchangeRates.ExchangeRatesConstants.CollectionName, cancellationToken);

        await using (var connection = store.Configuration.ConnectionFactory.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var schema = new SchemaBuilder(store.Configuration, transaction);

            await new TransactionMigrations { SchemaBuilder = schema }.CreateAsync();
            await new InvoicingMigrations { SchemaBuilder = schema }.CreateAsync();
            await new EstimatesMigrations { SchemaBuilder = schema }.CreateAsync();
            await new RecurringPlansMigrations { SchemaBuilder = schema }.CreateAsync();
            await new LateFeePolicyMigrations { SchemaBuilder = schema }.CreateAsync();
            await new BillableItemMigrations { SchemaBuilder = schema }.CreateAsync();
            await new UsageMigrations { SchemaBuilder = schema }.CreateAsync();
            await new CrestApps.OrchardCore.ExchangeRates.Migrations.ExchangeRateMigrations { SchemaBuilder = schema }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        InvoicingTestHarness harness = null;
        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        var locks = new LocalLock(NullLogger<LocalLock>.Instance);
        services.AddSingleton<IDistributedLock>(locks);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(Now));
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>(zone => zone.TimeZoneId == "UTC"));
        localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset utc) => utc);
        services.AddSingleton(localClock.Object);

        var site = new SiteSettings();
        site.Put(settings ?? new InvoicingSettings());
        site.Put(new CrestApps.OrchardCore.ExchangeRates.Models.ExchangeRateSettings { BaseCurrency = reportingCurrency });
        site.Put(new StoreProfileSettings { Name = "Contoso Supplies", Email = "billing@contoso.example" });
        services.AddSingleton(Mock.Of<ISiteService>(service => service.GetSiteSettingsAsync() == Task.FromResult<ISite>(site)));

        services.AddScoped<ITransactionStore>(sp => new BreakableTransactionStore(
            new TransactionStore(sp.GetRequiredService<ISession>(), sp.GetRequiredService<IClock>()),
            () => harness.FailNextTransaction && !(harness.FailNextTransaction = false)));
        services.AddScoped<ITransactionManager>(sp => new TransactionManager(sp.GetRequiredService<ITransactionStore>(), harness.TransactionHandlers, sp, NullLogger<CrestApps.Core.Services.CatalogManager<Transaction>>.Instance));
        services.AddScoped<ITransactionCommandService>(sp => new DefaultTransactionCommandService(
            sp.GetRequiredService<ITransactionManager>(),
            sp.GetRequiredService<IDistributedLock>(),
            sp.GetRequiredService<ISession>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            Mock.Of<IUserService>(),
            Mock.Of<IDisplayNameProvider>(),
            new TransactionDisplayText([], Options.Create(new TransactionSourceOptions()), new PassThroughStringLocalizer<TransactionDisplayText>(), new SourceTextLocalizerFactory()),
            sp,
            sp.GetRequiredService<IClock>(),
            NullLogger<DefaultTransactionCommandService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionCommandService>()));
        if (realNumbers)
        {
            // The series the Transactions feature registers, numbered as on a site: one sequence per unit of work, which
            // holds the series lock until that unit of work's scope ends.
            var seriesServices = new ServiceCollection();
            seriesServices.AddTransactionsCore();
            using var seriesProvider = seriesServices.BuildServiceProvider();
            var seriesOptions = Options.Create(seriesProvider.GetRequiredService<IOptions<NumberSeriesOptions>>().Value);
            var siteService = Mock.Of<ISiteService>(service => service.GetSiteSettingsAsync() == Task.FromResult<ISite>(site));

            services.AddScoped<INumberSequence>(sp => new DocumentNumberSequence(sp.GetRequiredService<ISession>(), numberLock ?? locks));
            services.AddScoped<INumberSeries>(sp => new NumberSeriesService(sp.GetRequiredService<INumberSequence>(), siteService, localClock.Object, seriesOptions));
            services.AddScoped<IFinancialDocumentNumberGenerator>(sp => new SequentialFinancialDocumentNumberGenerator(sp.GetRequiredService<INumberSeries>(), sp.GetRequiredService<ISession>()));
        }
        else
        {
            services.AddSingleton<IFinancialDocumentNumberGenerator>(_ => harness.Numbers);
            services.AddSingleton<INumberSeries>(_ => harness.Series);
        }
        services.AddSingleton(_ => harness.Contacts.Object);
        services.AddSingleton(_ => harness.SavedCards.Object);
        services.AddSingleton(_ => harness.Orders.Object);
        services.AddScoped<CrestApps.OrchardCore.Invoicing.Orders.OrderInvoiceBuilder>();
        services.AddScoped<CrestApps.OrchardCore.Invoicing.Renewals.RenewalInvoiceBuilder>();
        services.AddScoped<ISavedPaymentMethodChargeService>(sp =>
        {
            // The provider is played by ChargeOutcomes: a charge that goes through is recorded as the checkout records it.
            var commands = new Mock<ITransactionCommandService>();
            commands
                .Setup(service => service.ChargeSavedPaymentMethodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string transactionId, string cardId, CancellationToken token) =>
                {
                    harness.ChargedCards.Add(cardId);
                    var status = harness.ChargeOutcomes.Count > 0 ? harness.ChargeOutcomes.Dequeue() : SavedPaymentMethodChargeStatus.Succeeded;

                    if (status == SavedPaymentMethodChargeStatus.Succeeded)
                    {
                        var ledger = sp.GetRequiredService<ITransactionCommandService>();
                        var owed = (await sp.GetRequiredService<ITransactionManager>().FindByIdAsync(transactionId, token)).OutstandingAmount;
                        var paid = await ledger.RecordOfflinePaymentAsync(transactionId, owed, "card", "Charged to the saved card.", token);

                        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));

                        paid.ChargeStatus = status;

                        return paid;
                    }

                    var refused = new TransactionCommandResult { ChargeStatus = status };

                    return refused.Fail(status == SavedPaymentMethodChargeStatus.Failed ? "The provider could not be reached." : "The card was declined.");
                });

            return new DefaultSavedPaymentMethodChargeService(
                sp.GetRequiredService<ITransactionManager>(),
                commands.Object,
                sp.GetRequiredService<IDistributedLock>(),
                sp.GetRequiredService<ISession>(),
                sp.GetRequiredService<ISiteService>(),
                sp.GetRequiredService<IClock>(),
                sp,
                NullLogger<DefaultSavedPaymentMethodChargeService>.Instance,
                new PassThroughStringLocalizer<DefaultSavedPaymentMethodChargeService>());
        });
        services.AddSingleton(_ => harness.Credit.Object);
        services.AddSingleton<IInvoiceLedgerSyncScheduler>(_ => harness.Deferred);
        services.AddScoped<ITransactionEventHandler>(_ => new StatusHook(harness));
        services.AddScoped<IInvoiceObserver>(_ => new RecordingObserver(harness.Observed));
        services.AddSingleton<IBusinessDocumentPdfRenderer>(_ => harness.Pdf);
        services.AddSingleton(Mock.Of<IBusinessDocumentTemplateManager>());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddMoneyFormatter();
        services.AddSingleton(_ => new InvoiceArchiveFileStore(new FileSystemStore(harness.ArchiveFolder, NullLogger<FileSystemStore>.Instance)));
        services.AddSingleton<IInvoiceShareLinkService>(_ => new FakeShareLinks());

        var email = new Mock<ICommerceEmailSender>();
        email.SetupGet(sender => sender.CanSend).Returns(() => harness.CanSendEmail);
        email
            .Setup(sender => sender.SendWithResultAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (CommerceEmail message, Func<CancellationToken, Task<CommerceEmailContent>> build, CancellationToken token) =>
            {
                harness.Emails.Add((message, await build(token)));

                return CommerceEmailResult.Success();
            });
        services.AddSingleton(email.Object);

        services.AddInvoicingCore();
        services.AddInvoicingEstimates();
        services.AddInvoicingRecurring();
        services.AddInvoicingCollections();
        services.AddInvoicingBilling();
        CrestApps.OrchardCore.Transactions.Core.TransactionsServiceCollectionExtensions.AddPartyCollectionSettings(services);
        CrestApps.OrchardCore.ExchangeRates.Core.Services.ExchangeRatesServiceCollectionExtensions.AddExchangeRates(services);
        services.AddScoped<IRecurringPlanObserver>(_ => new RecordingPlanObserver(harness.Observed));
        services.AddScoped<IEstimateObserver>(_ => new RecordingEstimateObserver(harness.Observed));

        configureServices?.Invoke(services);
        harness = new InvoicingTestHarness(path, store, services.BuildServiceProvider());

        return harness;
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        T result;

        await using (var scope = _provider.CreateAsyncScope())
        {
            result = await work(scope.ServiceProvider);

            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunDeferredAsync();

        return result;
    }

    // The invoice mirrors deferred until the unit of work ended, each run in a scope of its own that saves what it changed.
    private async Task RunDeferredAsync()
    {
        while (Deferred.TryTake(out var transactionId))
        {
            await using var scope = _provider.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<InvoiceLedgerSync>().SyncTransactionAsync(transactionId, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Runs work as a request does: the session is saved when the work ends, even when it fails, as a site saves it when
    /// the request's scope ends. A command that fails must leave nothing in the session it doesn't mean to save.
    /// </summary>
    public async Task<T> InRequestAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        try
        {
            await using var scope = _provider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            try
            {
                return await work(scope.ServiceProvider);
            }
            finally
            {
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            await RunDeferredAsync();
        }
    }

    public Task<InvoiceCommandResult> RunAsync(Func<IInvoiceCommandService, Task<InvoiceCommandResult>> command)
        => InScopeAsync(services => command(services.GetRequiredService<IInvoiceCommandService>()));

    public async Task<Invoice> CreateDraftAsync(Action<Invoice> configure = null)
        => await InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IInvoiceManager>();
            var invoice = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            invoice.Currency = "USD";
            invoice.BillTo = new DocumentParty
            {
                Contact = new CrestApps.OrchardCore.Addresses.Models.ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" },
            };
            invoice.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m });

            configure?.Invoke(invoice);

            var validation = await manager.ValidateAsync(invoice, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(invoice, TestContext.Current.CancellationToken);

            return invoice;
        });

    public Task<Invoice> FindInvoiceAsync(string id)
        => InScopeAsync(services => services.GetRequiredService<IInvoiceStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    /// <summary>
    /// Counts the stored copies of an invoice: a command that saves an invoice the session no longer tracks writes a
    /// second document instead of updating the first.
    /// </summary>
    public Task<int> CountInvoiceDocumentsAsync(string id)
        => InScopeAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<InvoiceIndex>(index => index.ItemId == id, collection: InvoicingConstants.CollectionName)
            .CountAsync(TestContext.Current.CancellationToken));

    public Task<Transaction> FindTransactionAsync(string id)
        => InScopeAsync(services => services.GetRequiredService<ITransactionManager>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    public Task<TransactionCommandResult> OnLedgerAsync(Func<ITransactionCommandService, Task<TransactionCommandResult>> command)
        => InScopeAsync(services => command(services.GetRequiredService<ITransactionCommandService>()));

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        if (Directory.Exists(ArchiveFolder))
        {
            Directory.Delete(ArchiveFolder, recursive: true);
        }
    }

    /// <summary>
    /// Holds the transactions whose invoice mirror waits for the unit of work to end.
    /// </summary>
    internal sealed class AfterScopeSyncs : IInvoiceLedgerSyncScheduler
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pending = new();

        public int Scheduled { get; private set; }

        public void Schedule(string transactionId)
        {
            Scheduled++;
            _pending.Enqueue(transactionId);
        }

        public bool TryTake(out string transactionId)
            => _pending.TryDequeue(out transactionId);
    }

    private sealed class RecordingObserver : IInvoiceObserver
    {
        private readonly List<string> _observed;

        public RecordingObserver(List<string> observed)
        {
            _observed = observed;
        }

        public Task CreatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Created", invoice);

        public Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Issued", invoice);

        public Task SentAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Sent", invoice);

        public Task ViewedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Viewed", invoice);

        public Task PaymentReceivedAsync(Invoice invoice, decimal amount, CancellationToken cancellationToken = default)
            => Add("PaymentReceived", invoice, amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

        public Task PaidAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Paid", invoice);

        public Task OverdueAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Overdue", invoice);

        public Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Voided", invoice);

        public Task WrittenOffAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("WrittenOff", invoice);

        public Task ReinstatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Add("Reinstated", invoice);

        public Task CreditNoteIssuedAsync(CreditNote creditNote, Invoice invoice, CancellationToken cancellationToken = default)
            => Add("CreditNoteIssued", invoice, creditNote.Number);

        private Task Add(string kind, Invoice invoice, string detail = null)
        {
            lock (_observed)
            {
                _observed.Add($"{kind}:{invoice.Number ?? invoice.ItemId}" + (detail is null ? string.Empty : ":" + detail));
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class FakeNumberSeries : INumberSeries
    {
        private long _next = 1001;

        public Task<NumberSeriesFormat> GetFormatAsync(string series, CancellationToken cancellationToken = default)
            => Task.FromResult(new NumberSeriesFormat("EST-{seq}", 1001));

        public Task<SeriesNumber> NextAsync(string series, NumberSeriesRequest request = null, CancellationToken cancellationToken = default)
        {
            var sequence = _next++;

            return Task.FromResult(new SeriesNumber(sequence, $"EST-{sequence}"));
        }
    }

    private sealed class RecordingPlanObserver : IRecurringPlanObserver
    {
        private readonly List<string> _observed;

        public RecordingPlanObserver(List<string> observed)
        {
            _observed = observed;
        }

        public Task GeneratedAsync(RecurringPlan plan, Invoice invoice, RecurringPlanRun run, CancellationToken cancellationToken = default)
            => Add($"RecurringGenerated:{plan.Name}:{run.Occurrence:yyyy-MM-dd}");

        public Task RunFailedAsync(RecurringPlan plan, RecurringPlanRun run, CancellationToken cancellationToken = default)
            => Add($"RecurringFailed:{plan.Name}:{run.Occurrence:yyyy-MM-dd}");

        private Task Add(string entry)
        {
            lock (_observed)
            {
                _observed.Add(entry);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEstimateObserver : IEstimateObserver
    {
        private readonly List<string> _observed;

        public RecordingEstimateObserver(List<string> observed)
        {
            _observed = observed;
        }

        public Task SentAsync(Estimate estimate, CancellationToken cancellationToken = default)
            => Add("EstimateSent", estimate);

        public Task AcceptedAsync(Estimate estimate, CancellationToken cancellationToken = default)
            => Add("EstimateAccepted", estimate);

        public Task DeclinedAsync(Estimate estimate, CancellationToken cancellationToken = default)
            => Add("EstimateDeclined", estimate);

        public Task ExpiredAsync(Estimate estimate, CancellationToken cancellationToken = default)
            => Add("EstimateExpired", estimate);

        public Task ConvertedAsync(Estimate estimate, Invoice invoice, CancellationToken cancellationToken = default)
            => Add("EstimateConverted", estimate);

        private Task Add(string kind, Estimate estimate)
        {
            lock (_observed)
            {
                _observed.Add($"{kind}:{estimate.Number ?? estimate.ItemId}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StatusHook : ITransactionEventHandler
    {
        private readonly InvoicingTestHarness _harness;

        public StatusHook(InvoicingTestHarness harness)
        {
            _harness = harness;
        }

        public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
            => _harness.OnTransactionStatusChanged?.Invoke(transaction) ?? Task.CompletedTask;

        public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// A "PDF" that says what was rendered, so a test can tell an archived copy from a fresh rendering.
    /// </summary>
    internal sealed class FakePdfRenderer : IBusinessDocumentPdfRenderer
    {
        public int Renderings { get; private set; }

        public string Version { get; set; } = "v1";

        public Task<byte[]> RenderAsync(BusinessDocument document, CancellationToken cancellationToken = default)
        {
            Renderings++;

            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes($"{document.Kind}|{document.Number}|{document.Watermark}|{Version}"));
        }
    }

    /// <summary>
    /// Share links without a site URL: the invoice id in a fake link.
    /// </summary>
    internal sealed class FakeShareLinks : IInvoiceShareLinkService
    {
        public Task<string> CreateUrlAsync(Invoice invoice, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://contoso.example/invoices/view/{invoice.ItemId}");

        public string GetInvoiceId(string token)
            => token;
    }

    /// <summary>
    /// Numbers invoices INV-1001 and on, and counts what it gave out.
    /// </summary>
    internal sealed class FakeNumberGenerator : IFinancialDocumentNumberGenerator
    {
        private long _next = 1001;

        public int Issued { get; private set; }

        public Task<FinancialDocumentNumber> GenerateAsync(FinancialDocumentNumberRequest request, CancellationToken cancellationToken = default)
        {
            Issued++;
            var sequence = _next++;

            return Task.FromResult(new FinancialDocumentNumber(sequence, $"INV-{sequence}"));
        }
    }
}
