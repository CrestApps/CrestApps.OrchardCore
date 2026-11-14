using CrestApps.Core.Models;
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

    public static async Task<InvoicingTestHarness> CreateAsync(InvoicingSettings settings = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-invoicing-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

        store.RegisterIndexes([new TransactionIndexProvider()], TransactionsConstants.CollectionName);
        store.RegisterIndexes([new InvoiceIndexProvider(), new CreditNoteIndexProvider()], InvoicingConstants.CollectionName);

        var cancellationToken = TestContext.Current.CancellationToken;

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(TransactionsConstants.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(InvoicingConstants.CollectionName, cancellationToken);

        await using (var connection = store.Configuration.ConnectionFactory.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var schema = new SchemaBuilder(store.Configuration, transaction);

            await new TransactionMigrations { SchemaBuilder = schema }.CreateAsync();
            await new InvoicingMigrations { SchemaBuilder = schema }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        InvoicingTestHarness harness = null;
        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(Now));
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>(zone => zone.TimeZoneId == "UTC"));
        services.AddSingleton(localClock.Object);

        var site = new SiteSettings();
        site.Put(settings ?? new InvoicingSettings());
        site.Put(new StoreProfileSettings { Name = "Contoso Supplies", Email = "billing@contoso.example" });
        services.AddSingleton(Mock.Of<ISiteService>(service => service.GetSiteSettingsAsync() == Task.FromResult<ISite>(site)));

        services.AddScoped<ITransactionStore, TransactionStore>();
        services.AddScoped<ITransactionManager>(sp => new TransactionManager(sp.GetRequiredService<ITransactionStore>(), [], sp, NullLogger<CrestApps.Core.Services.CatalogManager<Transaction>>.Instance));
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
        services.AddSingleton<IFinancialDocumentNumberGenerator>(_ => harness.Numbers);
        services.AddSingleton(_ => harness.Contacts.Object);
        services.AddSingleton(_ => harness.Credit.Object);
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

        harness = new InvoicingTestHarness(path, store, services.BuildServiceProvider());

        return harness;
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();

        var result = await work(scope.ServiceProvider);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
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
