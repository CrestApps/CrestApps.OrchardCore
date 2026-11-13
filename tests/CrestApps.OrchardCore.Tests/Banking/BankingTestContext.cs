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

        Store = new InMemoryBankAccountStore();
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
            HttpContextAccessor,
            clock.Object,
            RecipeRoundTrip.Localizer<BankAccountService>());
    }

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
internal sealed class InMemoryBankAccountStore : InMemoryCatalog<BankAccount>, IBankAccountStore
{
    public Task<IReadOnlyList<BankAccount>> ListByOwnersAsync(IEnumerable<PartyRef> owners, CancellationToken cancellationToken = default)
    {
        var wanted = owners.ToHashSet();

        return Task.FromResult<IReadOnlyList<BankAccount>>(Entries.Where(entry => wanted.Contains(entry.Owner)).ToList());
    }
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
