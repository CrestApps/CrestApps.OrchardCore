using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Disbursements.Core.Handlers;
using CrestApps.OrchardCore.Disbursements.Core.Nacha;
using CrestApps.OrchardCore.Disbursements.Core.Services;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Banking;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The disbursement services over in-memory stores, on top of the bank account and check services, with the manual,
/// bank file and check providers and the prenote verifier.
/// </summary>
internal sealed class DisbursementsTestContext
{
    public const string OriginRoutingNumber = BankingTestContext.ValidRoutingNumber;

    public DisbursementsTestContext()
    {
        Checks = new ChecksTestContext();

        var clock = new Mock<IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(() => Banking.Now);

        Store = new InMemoryDisbursementStore();
        BatchStore = new InMemoryDisbursementBatchStore();
        Files = new InMemoryDisbursementFileStore();
        Observer = new RecordingDisbursementObserver();
        Manager = new DisbursementManager(Store, [new DisbursementHandler(clock.Object)], NullLogger<CatalogManager<Disbursement>>.Instance);
        Batches = new DisbursementBatchManager(BatchStore, [new DisbursementBatchHandler(clock.Object)], NullLogger<CatalogManager<DisbursementBatch>>.Instance);

        var services = new ServiceCollection();

        services.AddSingleton<IBankAccountManager>(Banking.Manager);
        services.AddSingleton<IBankAccountService>(Banking.Service);
        services.AddSingleton<IDisbursementManager>(Manager);
        services.AddSingleton<IDisbursementService>(provider => Service);
        Services = services.BuildServiceProvider();

        var providers = new List<IDisbursementProvider>
        {
            new ManualDisbursementProvider(RecipeRoundTrip.Localizer<ManualDisbursementProvider>()),
            new NachaDisbursementProvider(Banking.Manager, Banking.Service, Batches, new InMemoryNumberSequence(), Checks.LocalClock, clock.Object, RecipeRoundTrip.Localizer<NachaDisbursementProvider>()),
            new CheckDisbursementProvider(Checks.Service, RecipeRoundTrip.Localizer<CheckDisbursementProvider>()),
        };

        Service = new DisbursementService(
            Manager,
            Batches,
            BatchStore,
            Files,
            Banking.Manager,
            Banking.Service,
            providers,
            [Observer, new NachaPrenoteObserver(Banking.Manager, Banking.Service)],
            Banking.HttpContextAccessor,
            clock.Object,
            RecipeRoundTrip.Localizer<DisbursementService>());

        Banking.Usages.Add(new DisbursementBankAccountUsage(Store, RecipeRoundTrip.Localizer<DisbursementBankAccountUsage>()));
        Checks.Observers.Add(new CheckDisbursementObserver(Services));
    }

    public ChecksTestContext Checks { get; }

    public BankingTestContext Banking => Checks.Banking;

    public InMemoryDisbursementStore Store { get; }

    public InMemoryDisbursementBatchStore BatchStore { get; }

    public InMemoryDisbursementFileStore Files { get; }

    public RecordingDisbursementObserver Observer { get; }

    public DisbursementManager Manager { get; }

    public DisbursementBatchManager Batches { get; }

    public DisbursementService Service { get; }

    public IServiceProvider Services { get; }

    public NachaPrenoteVerifier PrenoteVerifier()
        => new(Services, RecipeRoundTrip.Localizer<NachaPrenoteVerifier>());

    /// <summary>
    /// Adds one of the business's accounts, set up for bank files.
    /// </summary>
    public async Task<BankAccount> OriginAsync(bool offset = false)
    {
        var account = await Banking.CreateAsync("900000001", name: "Payroll funding");

        account.Put(new NachaOriginSettings
        {
            Enabled = true,
            ImmediateDestinationName = "FIRST BANK",
            ImmediateOrigin = "1123456789",
            ImmediateOriginName = "CONTOSO LTD",
            CompanyName = "CONTOSO LTD",
            CompanyIdentification = "1123456789",
            CompanyEntryDescription = "PAYROLL",
            IncludeOffset = offset,
        });

        await Banking.Manager.UpdateAsync(account);

        return account;
    }

    /// <summary>
    /// Adds a payee's account, verified unless asked.
    /// </summary>
    public async Task<BankAccount> PayeeAsync(string accountNumber = "123456789", bool verified = true, string staffId = "pat")
    {
        var account = await Banking.CreateAsync(accountNumber, new PartyRef("Staff", staffId), "Pay account");

        if (verified)
        {
            Assert.True(await Banking.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, "Called the bank"));
        }

        return account;
    }

    public async Task<Disbursement> AchAsync(BankAccount origin, BankAccount payee, decimal amount = 1250.50m, string sourceId = "statement-1", string scope = null)
    {
        var result = await Service.CreateAsync(new DisbursementRequest
        {
            Payee = payee.Owner,
            PayeeName = "Pat Doe",
            Amount = amount,
            Method = "Ach",
            SourceBankAccountId = origin.ItemId,
            DestinationBankAccountId = payee.ItemId,
            SourceKind = "PayStatement",
            SourceId = sourceId,
            AccessScope = scope,
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Disbursement;
    }
}

/// <summary>
/// Disbursements held in memory.
/// </summary>
internal sealed class InMemoryDisbursementStore : InMemoryCatalog<Disbursement>, IDisbursementStore
{
    public Task<Disbursement> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.IdempotencyKey == key));

    public Task<Disbursement> FindByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.TraceNumber == traceNumber));

    public Task<Disbursement> FindByCheckIdAsync(string checkId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.CheckId == checkId));

    public Task<IReadOnlyList<Disbursement>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Disbursement>>(Entries.Where(entry => entry.SourceKind == sourceKind && entry.SourceId == sourceId).ToList());

    public Task<bool> UsesBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Any(entry => entry.SourceBankAccountId == bankAccountId || entry.DestinationBankAccountId == bankAccountId));

    public Task<(int Count, IReadOnlyList<Disbursement> Disbursements)> ListPageAsync(DisbursementQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        // The database runs SourceAccessExpressions; DisbursementStoreTests proves it agrees with this rule on SQLite.
        var matching = Entries
            .Where(entry => (query.Access ?? global::CrestApps.OrchardCore.SourceAccess.SourceAccessFilter.Unrestricted).Allows(entry))
            .Where(entry => query.Status is null || entry.Status == query.Status)
            .Where(entry => string.IsNullOrEmpty(query.BatchId) || entry.BatchId == query.BatchId)
            .ToList();
        var ordered = query.OldestFirst ? matching.OrderBy(entry => entry.CreatedUtc) : matching.OrderByDescending(entry => entry.CreatedUtc);

        return Task.FromResult<(int, IReadOnlyList<Disbursement>)>((matching.Count, ordered.Skip(skip).Take(take).ToList()));
    }
}

/// <summary>
/// Disbursement batches held in memory.
/// </summary>
internal sealed class InMemoryDisbursementBatchStore : InMemoryCatalog<DisbursementBatch>, IDisbursementBatchStore
{
    public Task<IReadOnlyList<DisbursementBatch>> ListReleasedAsync(string provider, DateTime releasedAfterUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DisbursementBatch>>(Entries.Where(entry => entry.Provider == provider && entry.ReleasedUtc > releasedAfterUtc).OrderBy(entry => entry.ReleasedUtc).ToList());

    public Task<int> CountReleasedAsync(string sourceBankAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Count(entry => entry.SourceBankAccountId == sourceBankAccountId && entry.ReleasedUtc >= fromUtc && entry.ReleasedUtc < toUtc));

    public Task<IReadOnlyList<DisbursementBatch>> ListToSettleAsync(DateTime releasedBeforeUtc, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DisbursementBatch>>(Entries.Where(entry => entry.Status == DisbursementBatchStatus.Released && entry.ReleasedUtc < releasedBeforeUtc).Take(take).ToList());

    public Task<(int Count, IReadOnlyList<DisbursementBatch> Batches)> ListPageAsync(DisbursementBatchStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var matching = Entries.Where(entry => status is null || entry.Status == status).OrderByDescending(entry => entry.CreatedUtc).ToList();

        return Task.FromResult<(int, IReadOnlyList<DisbursementBatch>)>((matching.Count, matching.Skip(skip).Take(take).ToList()));
    }
}

/// <summary>
/// Batch files held in memory.
/// </summary>
internal sealed class InMemoryDisbursementFileStore : IDisbursementFileStore
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public Task WriteAsync(string batchId, byte[] content)
    {
        Files[batchId] = content;

        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string batchId, CancellationToken cancellationToken = default)
        => Task.FromResult(Files.GetValueOrDefault(batchId));
}

/// <summary>
/// Records the disbursement changes it is told about.
/// </summary>
internal sealed class RecordingDisbursementObserver : IDisbursementObserver
{
    public List<DisbursementChange> Changes { get; } = [];

    public Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}
