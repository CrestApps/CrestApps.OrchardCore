using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Checks.Core.Handlers;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Banking;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The check services over in-memory stores, on top of the bank account services.
/// </summary>
internal sealed class ChecksTestContext
{
    public ChecksTestContext()
    {
        var clock = new Mock<IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(() => Banking.Now);

        Banking = new BankingTestContext();
        Store = new InMemoryCheckRecordStore();
        Files = new InMemoryCheckFileStore();
        Numbers = new InMemoryNumberSequence();
        Observer = new RecordingCheckObserver();
        LocalClock = new FixedOffsetLocalClock(() => Banking.Now, TimeSpan.Zero);
        Manager = new CheckManager(Store, [new CheckRecordHandler(clock.Object)], NullLogger<CatalogManager<CheckRecord>>.Instance);
        Observers = [Observer];
        Service = new CheckService(Manager, Banking.Manager, Numbers, Observers, Handlers, WriteHandlers, Locks, Committer, new HttpContextAccessor(), clock.Object, LocalClock, RecipeRoundTrip.Localizer<CheckService>());
        Banking.Usages.Add(new CheckBankAccountUsage(Store, RecipeRoundTrip.Localizer<CheckBankAccountUsage>()));
    }

    /// <summary>
    /// Gets the site's local clock, at UTC unless a test moves it.
    /// </summary>
    public FixedOffsetLocalClock LocalClock { get; }

    public BankingTestContext Banking { get; }

    public InMemoryCheckRecordStore Store { get; }

    public InMemoryCheckFileStore Files { get; }

    public InMemoryNumberSequence Numbers { get; }

    public RecordingCheckObserver Observer { get; }

    /// <summary>
    /// Gets the observers the check service tells; tests add to it.
    /// </summary>
    public List<ICheckObserver> Observers { get; }

    /// <summary>
    /// Gets the handlers the check service asks before a change; tests add to it.
    /// </summary>
    public List<ICheckChangeHandler> Handlers { get; } = [];

    /// <summary>
    /// Gets the handlers that take part in writing a check; tests add to it.
    /// </summary>
    public List<ICheckWriteHandler> WriteHandlers { get; } = [];

    /// <summary>
    /// Gets the locks the check service takes; a test may hold one.
    /// </summary>
    public KeyedDistributedLock Locks { get; } = new();

    /// <summary>
    /// Gets the committer, which counts the commits the check service asks for.
    /// </summary>
    public CountingCheckCommitter Committer { get; } = new();

    public CheckManager Manager { get; }

    public CheckService Service { get; }

    public CheckPrinter Printer(params ICheckVoucherSource[] voucherSources)
        => new(
            Banking.Manager,
            Banking.Service,
            Files,
            voucherSources,
            [new UnitedStatesCheckMicrFormat()],
            [new EnglishAmountInWords()],
            Array.Empty<IAddressFormatter>(),
            RecipeRoundTrip.Localizer<CheckPrinter>());

    public async Task<BankAccount> BusinessAccountAsync(string accountNumber = "123456789", string name = "Operating")
        => await Banking.CreateAsync(accountNumber, name: name);

    public async Task<CheckRecord> IssueAsync(BankAccount account, decimal amount = 1234.56m, string payee = "Pat Doe")
    {
        var result = await Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = payee,
            Amount = amount,
            CheckDate = new DateOnly(2026, 11, 13),
            Memo = "Test",
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    public static PartyRef Staff(string id)
        => new("Staff", id);
}

/// <summary>
/// The check register held in memory.
/// </summary>
internal sealed class InMemoryCheckRecordStore : InMemoryCatalog<CheckRecord>, ICheckRecordStore
{
    public Task<CheckRecord> FindByNumberAsync(string bankAccountId, long number, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.BankAccountId == bankAccountId && entry.Number == number));

    public Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Where(entry => entry.BankAccountId == bankAccountId).Select(entry => (long?)entry.Number).Max());

    public Task<IReadOnlyList<CheckRecord>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CheckRecord>>(Entries.Where(entry => entry.SourceKind == sourceKind && entry.SourceId == sourceId).ToList());

    public Task<(int Count, IReadOnlyList<CheckRecord> Checks)> ListPageAsync(CheckRegisterQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        // The database runs CheckAccessExpressions; CheckRegisterStoreTests proves it agrees with this rule on SQLite.
        var access = query.Access ?? SourceAccessFilter.Unrestricted;
        var matching = Entries
            .Where(entry => access.Allows(entry.SourceKind, entry.AccessScope))
            .Where(entry => string.IsNullOrEmpty(query.BankAccountId) || entry.BankAccountId == query.BankAccountId)
            .Where(entry => query.Status is null || entry.Status == query.Status)
            .ToList();
        var ordered = query.OldestFirst
            ? matching.OrderBy(entry => entry.IssuedUtc).ThenBy(entry => entry.Number)
            : matching.OrderByDescending(entry => entry.IssuedUtc).ThenByDescending(entry => entry.Number);

        return Task.FromResult<(int, IReadOnlyList<CheckRecord>)>((matching.Count, ordered.Skip(skip).Take(take).ToList()));
    }

    public Task<IReadOnlyList<CheckRecord>> ListPrintedBeforeAsync(DateOnly checkDateBefore, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CheckRecord>>(Entries
            .Where(entry => entry.Status == CheckStatus.Printed && entry.CheckDate < checkDateBefore)
            .OrderBy(entry => entry.CheckDate)
            .ThenBy(entry => entry.Number)
            .Take(take)
            .ToList());
}

/// <summary>
/// A source access that lets each user see the access scopes it lists for them.
/// </summary>
internal sealed class FixedSourceAccess : ISourceAccess
{
    private readonly Dictionary<string, SourceAccessScope> _scopes;

    public FixedSourceAccess(string sourceKind, Dictionary<string, SourceAccessScope> scopesByUserName)
    {
        SourceKind = sourceKind;
        _scopes = scopesByUserName;
    }

    public string SourceKind { get; }

    public Task<SourceAccessScope> GetScopeAsync(System.Security.Claims.ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
        => Task.FromResult(_scopes.GetValueOrDefault(user?.Identity?.Name ?? string.Empty) ?? SourceAccessScope.None);
}

/// <summary>
/// Layout files held in memory.
/// </summary>
internal sealed class InMemoryCheckFileStore : ICheckFileStore
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public Task WriteAsync(string layoutId, string kind, byte[] content)
    {
        Files[layoutId + "/" + kind] = content;

        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string layoutId, string kind, CancellationToken cancellationToken = default)
        => Task.FromResult(Files.GetValueOrDefault(layoutId + "/" + kind));

    public Task DeleteAsync(string layoutId, string kind)
    {
        Files.Remove(layoutId + "/" + kind);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Records the check changes it is told about.
/// </summary>
internal sealed class RecordingCheckObserver : ICheckObserver
{
    public List<CheckChange> Changes { get; } = [];

    public Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}
