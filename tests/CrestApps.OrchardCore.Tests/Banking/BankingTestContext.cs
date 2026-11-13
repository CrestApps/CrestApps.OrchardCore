using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Core.Handlers;
using CrestApps.OrchardCore.Banking.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// The bank account services over an in-memory store, with an observer that records every change it is told about.
/// </summary>
internal sealed class BankingTestContext
{
    public const string ValidRoutingNumber = "021000021";

    public const string OtherValidRoutingNumber = "011000015";

    public BankingTestContext(IEnumerable<IBankAccountVerifier> verifiers = null)
    {
        var clock = new Mock<IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(() => Now);

        Store = new InMemoryBankAccountStore(Usages);
        Tracker = new BankAccountChangeTracker();
        Observer = new RecordingBankAccountObserver();
        Protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());
        HttpContextAccessor = new HttpContextAccessor();

        var handler = new BankAccountHandler(Tracker, [Observer], HttpContextAccessor, clock.Object, RecipeRoundTrip.Localizer<BankAccountHandler>());

        Manager = new BankAccountManager(Store, [handler], NullLogger<CatalogManager<BankAccount>>.Instance);
        Service = new BankAccountService(
            Protector,
            [new UnitedStatesBankRoutingValidator()],
            verifiers ?? [],
            Manager,
            Tracker,
            [Observer],
            Usages,
            HttpContextAccessor,
            clock.Object,
            Logger,
            RecipeRoundTrip.Localizer<BankAccountService>());
    }

    /// <summary>
    /// Gets the modules whose records point to accounts; tests add to it.
    /// </summary>
    public List<IBankAccountUsage> Usages { get; } = [];

    /// <summary>
    /// Gets the log of the bank account service.
    /// </summary>
    public RecordingLogger<BankAccountService> Logger { get; } = new();

    public DateTime Now { get; set; } = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    public InMemoryBankAccountStore Store { get; }

    public BankAccountChangeTracker Tracker { get; }

    public RecordingBankAccountObserver Observer { get; }

    public DataProtectionSensitiveValueProtector Protector { get; }

    public HttpContextAccessor HttpContextAccessor { get; }

    public BankAccountManager Manager { get; }

    public BankAccountService Service { get; }

    /// <summary>
    /// Adds an account the way the editor does: details, then numbers, then the save.
    /// </summary>
    public async Task<BankAccount> CreateAsync(string accountNumber = "123456789", PartyRef owner = default, string name = "Main checking")
    {
        var account = await Manager.NewAsync();

        account.Name = name;
        account.HolderName = "Contoso Ltd";
        account.Owner = owner;
        account.BankName = "First Bank";

        Assert.Empty(Service.ApplyNumbers(account, ValidRoutingNumber, accountNumber));
        Assert.True((await Manager.ValidateAsync(account)).Succeeded);

        await Manager.CreateAsync(account);

        return account;
    }
}

/// <summary>
/// Bank accounts held in memory.
/// </summary>
internal sealed class InMemoryBankAccountStore : InMemoryCatalog<BankAccount>, IBankAccountStore, ICatalog<BankAccount>
{
    private readonly IEnumerable<IBankAccountUsage> _usages;

    public InMemoryBankAccountStore(IEnumerable<IBankAccountUsage> usages = null)
    {
        _usages = usages ?? [];
    }

    // As the YesSql store does: an account records point to is never deleted.
    public new async ValueTask<bool> DeleteAsync(BankAccount entry, CancellationToken cancellationToken = default)
    {
        var reasons = await BankAccountUsages.GetReasonsAsync(_usages, entry, cancellationToken);

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException("The bank account is in use: " + string.Join(" ", reasons));
        }

        return await base.DeleteAsync(entry, cancellationToken);
    }

    public Task<IReadOnlyList<BankAccount>> ListByOwnersAsync(IEnumerable<PartyRef> owners, CancellationToken cancellationToken = default)
    {
        var wanted = owners.ToHashSet();

        return Task.FromResult<IReadOnlyList<BankAccount>>(Entries.Where(entry => wanted.Contains(entry.Owner)).ToList());
    }
}

/// <summary>
/// Keeps the messages written to it.
/// </summary>
/// <typeparam name="T">The category.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel)
        => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}

/// <summary>
/// A module whose records point to the accounts it lists.
/// </summary>
internal sealed class FixedBankAccountUsage : IBankAccountUsage
{
    private readonly HashSet<string> _accountIds;
    private readonly string _reason;

    public FixedBankAccountUsage(string reason, params string[] accountIds)
    {
        _reason = reason;
        _accountIds = new HashSet<string>(accountIds, StringComparer.Ordinal);
    }

    public Task<string> GetUsageAsync(BankAccount account, CancellationToken cancellationToken = default)
        => Task.FromResult(_accountIds.Contains(account.ItemId) ? _reason : null);
}

/// <summary>
/// Records the bank account changes it is told about.
/// </summary>
internal sealed class RecordingBankAccountObserver : IBankAccountObserver
{
    public List<BankAccountChange> Changes { get; } = [];

    public Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}
