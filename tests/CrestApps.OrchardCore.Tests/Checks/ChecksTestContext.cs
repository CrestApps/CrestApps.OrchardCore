using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Checks.Core.Handlers;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Parties.Models;
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
        Manager = new CheckManager(Store, [new CheckRecordHandler(clock.Object)], NullLogger<CatalogManager<CheckRecord>>.Instance);
        Service = new CheckService(Manager, Banking.Manager, Numbers, [Observer], new HttpContextAccessor(), clock.Object, RecipeRoundTrip.Localizer<CheckService>());
    }

    public BankingTestContext Banking { get; }

    public InMemoryCheckRecordStore Store { get; }

    public InMemoryCheckFileStore Files { get; }

    public InMemoryNumberSequence Numbers { get; }

    public RecordingCheckObserver Observer { get; }

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
