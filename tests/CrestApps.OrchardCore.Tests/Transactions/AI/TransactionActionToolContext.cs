using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// What the tests of the transaction actions share: a transaction of 100 USD owed by the account Northwind (account-7),
/// with a 40 USD check recorded by hand and a 10 USD discount on its timeline, doubles of the services the tools call,
/// one distributed cache and lock for the preview tokens and idempotency keys of a test, and the tools' registrations.
/// </summary>
internal sealed class TransactionActionToolContext
{
    public static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    public TransactionActionToolContext()
    {
        Transaction = new Transaction
        {
            ItemId = "tx-1",
            Title = "Invoice INV-0042",
            Currency = "USD",
            TotalAmount = 100m,
            AmountPaid = 40m,
            AmountAdjusted = 10m,
            Status = TransactionStatus.PartiallyPaid,
        };
        Transaction.SetPayer(new PartyRef(PartyKinds.Account, "account-7"));
        Transaction.Events.Add(new TransactionEvent
        {
            Id = "check-1",
            Type = TransactionEventType.PaymentRecorded,
            Amount = 40m,
            CreatedUtc = Now.AddDays(-3),
            PaymentMethod = "check",
            Reference = "CHK-311",
            ActorName = "pat",
        });
        Transaction.Events.Add(new TransactionEvent
        {
            Id = "discount-1",
            Type = TransactionEventType.Adjusted,
            Amount = 10m,
            AdjustmentKind = TransactionAdjustmentKind.Discount,
            CreatedUtc = Now.AddDays(-2),
        });

        Transactions.Setup(manager => manager.FindByIdAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => Transaction);
        Credit.Setup(credit => credit.CanHold(It.IsAny<PartyRef>())).Returns((PartyRef party) => !party.IsEmpty && party.Kind != PartyKinds.Guest);
        Credit.Setup(credit => credit.GetBalanceAsync(It.IsAny<PartyRef>(), "USD", It.IsAny<CancellationToken>())).ReturnsAsync(() => CreditBalance);
        Parties.Setup(directory => directory.GetNameAsync(new PartyRef(PartyKinds.Account, "account-7"), It.IsAny<CancellationToken>())).ReturnsAsync("Northwind Traders");
    }

    /// <summary>
    /// Gets or sets the transaction the tools find as <c>tx-1</c>.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the payer's customer credit in USD.
    /// </summary>
    public decimal CreditBalance { get; set; } = 25m;

    public Mock<ITransactionManager> Transactions { get; } = new();

    public Mock<ITransactionCommandService> Commands { get; } = new();

    public Mock<ICustomerCreditService> Credit { get; } = new();

    public Mock<ISavedPaymentMethodChargeService> Charges { get; } = new();

    public Mock<IPartyCollectionSettingsService> CollectionSettings { get; } = new();

    public Mock<IPartyDirectory> Parties { get; } = new();

    /// <summary>
    /// Gets the distributed cache of the test's preview tokens and idempotency keys.
    /// </summary>
    public IDistributedCache Cache { get; } = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <param name="signedIn">Whether a user is signed in.</param>
    /// <param name="authorize">The rule that grants a permission; everything by default.</param>
    /// <param name="withCredit">Whether the Store Credit feature keeps customer credit.</param>
    public ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null, bool withCredit = true)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(Transactions.Object);
        services.AddSingleton(Commands.Object);
        services.AddSingleton(Charges.Object);
        services.AddSingleton(CollectionSettings.Object);
        services.AddSingleton(Parties.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton(Cache);
        services.AddSingleton<IDistributedLock>(new FakeDistributedLock());
        services.AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == Now));

        if (withCredit)
        {
            services.AddSingleton(Credit.Object);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Calls a tool and returns its answer.
    /// </summary>
    public static Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(tool, services, arguments);

    /// <summary>
    /// Previews an action and returns its token, failing with the answer when there is none.
    /// </summary>
    public static async Task<string> PreviewAsync(IServiceProvider services, Dictionary<string, object> arguments)
    {
        var answer = await InvokeAsync(new PreviewTransactionActionTool(), services, arguments);

        Assert.True(answer.StartsWith('{'), answer);

        return AgentToolTestContext.ReadJson(answer).GetProperty("previewToken").GetString();
    }

    /// <summary>
    /// Gets the transaction tools every Transactions AI startup registers.
    /// </summary>
    public static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        new AIToolsStartup(new PassThroughStringLocalizer<AIToolsStartup>()).ConfigureServices(services);
        new TransactionActionsAIToolsStartup(new PassThroughStringLocalizer<TransactionActionsAIToolsStartup>()).ConfigureServices(services);
        new TransactionCreditAIToolsStartup(new PassThroughStringLocalizer<TransactionCreditAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    /// <summary>
    /// Gets whether a transaction tool is registered to ask for approval.
    /// </summary>
    public static bool RequiresApproval(string toolName)
        => GetTools()[toolName].RequiresApproval;
}
