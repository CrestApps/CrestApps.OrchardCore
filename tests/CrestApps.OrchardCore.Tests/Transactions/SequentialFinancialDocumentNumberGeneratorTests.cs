using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Models;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Receipt numbers are short and in order, and a number is never issued twice. These run against a real store,
/// because what matters is what the next issuer reads back after the last one saved.
/// </summary>
public sealed class SequentialFinancialDocumentNumberGeneratorTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-numbers-{Guid.NewGuid():N}.db");
    private readonly LocalLock _locks = new(NullLogger<LocalLock>.Instance);
    private readonly NumberSeriesSettings _settings = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));

        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(TransactionsConstants.CollectionName, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GenerateAsync_IssuesShortNumbersInOrder_AndEachKindKeepsItsOwnSeries()
    {
        // Act
        var receipts = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            receipts.Add((await GenerateAsync(FinancialDocumentKind.Receipt)).PublicToken);
        }

        var invoice = await GenerateAsync(FinancialDocumentKind.Invoice);
        var nextReceipt = await GenerateAsync(FinancialDocumentKind.Receipt);

        // Assert
        Assert.Equal(["R-1001", "R-1002", "R-1003"], receipts);
        Assert.Equal("INV-1001", invoice.PublicToken);
        Assert.Equal(SequentialFinancialDocumentNumberGenerator.FirstNumber, invoice.Sequence);
        Assert.Equal("R-1004", nextReceipt.PublicToken);
    }

    [Fact]
    public async Task GenerateAsync_CarriesOnAfterTheNumbersIssuedBeforeTheSharedSeries()
    {
        // Arrange
        await using (var session = _store.CreateSession())
        {
            var legacy = new FinancialDocumentSequences();

            legacy.Values[nameof(FinancialDocumentKind.Receipt)] = 1240;
            legacy.Values[nameof(FinancialDocumentKind.Invoice)] = 1017;

            await session.SaveAsync(legacy, collection: TransactionsConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var receipt = await GenerateAsync(FinancialDocumentKind.Receipt);
        var secondReceipt = await GenerateAsync(FinancialDocumentKind.Receipt);
        var invoice = await GenerateAsync(FinancialDocumentKind.Invoice);
        var creditNote = await GenerateAsync(FinancialDocumentKind.CreditNote);

        // Assert
        Assert.Equal("R-1241", receipt.PublicToken);
        Assert.Equal("R-1242", secondReceipt.PublicToken);
        Assert.Equal("INV-1018", invoice.PublicToken);
        Assert.Equal("CN-1001", creditNote.PublicToken);
    }

    [Fact]
    public async Task GenerateAsync_WritesTheNumberInTheFormatTheSiteChose()
    {
        // Arrange
        _settings.Formats[FinancialDocumentSeries.GetName(FinancialDocumentKind.Invoice)] = new NumberSeriesFormat("F-{seq:000000}", firstNumber: 50);

        // Act
        var invoice = await GenerateAsync(FinancialDocumentKind.Invoice);

        // Assert
        Assert.Equal("F-000050", invoice.PublicToken);
    }

    [Fact]
    public async Task GenerateAsync_ANumberTakenByWorkThatFails_IsGivenToTheNextIssuer()
    {
        // Arrange
        await GenerateAsync(FinancialDocumentKind.Invoice);

        // Act
        await GenerateAsync(FinancialDocumentKind.Invoice, save: false);

        var next = await GenerateAsync(FinancialDocumentKind.Invoice);

        // Assert
        Assert.Equal("INV-1002", next.PublicToken);
    }

    private async Task<FinancialDocumentNumber> GenerateAsync(FinancialDocumentKind kind, bool save = true)
    {
        // Each document is numbered by its own unit of work, as each payment is.
        await using var session = _store.CreateSession();
        await using var sequence = new DocumentNumberSequence(session, _locks);

        var generator = new SequentialFinancialDocumentNumberGenerator(CreateSeries(sequence), session);
        var number = await generator.GenerateAsync(new FinancialDocumentNumberRequest(kind), TestContext.Current.CancellationToken);

        if (save)
        {
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await session.CancelAsync();
        }

        return number;
    }

    private NumberSeriesService CreateSeries(INumberSequence sequence)
    {
        // The series are the ones the Transactions feature registers.
        var services = new ServiceCollection();

        services.AddTransactionsCore();

        using var provider = services.BuildServiceProvider();

        var site = new SiteSettings();

        site.Put(_settings);

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero));

        return new NumberSeriesService(sequence, siteService.Object, localClock.Object, provider.GetRequiredService<IOptions<NumberSeriesOptions>>());
    }
}
