using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
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
/// The transaction and payment ledger tools refuse a caller who is not signed in or lacks the page's permission, follow
/// the owner's money rules (an amount in the record's currency, an explicit flag for the whole balance, more than is owed
/// only when it is kept as credit, and an idempotency key), record a payment with how and when it was received as the
/// Record payment form does, show what the transaction's page shows, refuse to refund a payment that belongs to an order,
/// and require a reference to settle a refund by hand.
/// </summary>
public sealed class TransactionToolTests
{
    private readonly Transaction _transaction = new()
    {
        ItemId = "tx-1",
        Title = "Invoice INV-0042",
        Currency = "USD",
        TotalAmount = 100m,
        AmountPaid = 40m,
        Status = TransactionStatus.PartiallyPaid,
    };

    private readonly PaymentAttempt _orderPayment = new()
    {
        ItemId = "attempt-1",
        ReferenceType = CheckoutReferenceTypes.Order,
        ReferenceId = "order-1",
        State = PaymentAttemptState.Succeeded,
        TransactionId = "pi_1",
        Currency = "USD",
        ConfirmedAmount = 55m,
    };

    private readonly Mock<ITransactionManager> _transactions = new();
    private readonly Mock<ITransactionCommandService> _commands = new();
    private readonly Mock<IPaymentAttemptStore> _attempts = new();
    private readonly Mock<IPaymentRefundStore> _refunds = new();
    private readonly Mock<IPaymentIssueStore> _issues = new();
    private readonly Mock<ICheckoutRefundService> _refundService = new();
    private readonly DateTime _now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    public TransactionToolTests()
    {
        _transactions.Setup(manager => manager.FindByIdAsync("tx-1", It.IsAny<CancellationToken>())).ReturnsAsync(_transaction);
        _attempts.Setup(store => store.FindByIdAsync("attempt-1", It.IsAny<CancellationToken>())).ReturnsAsync(_orderPayment);
        _commands.Setup(commands => commands.RecordPaymentAsync("tx-1", It.IsAny<TransactionPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, TransactionPaymentRequest request, CancellationToken _) => new TransactionCommandResult
            {
                Transaction = _transaction,
                Event = new TransactionEvent
                {
                    Amount = Math.Min(request.Amount, 60m),
                    Type = TransactionEventType.PaymentRecorded,
                    PaymentMethod = request.PaymentMethod,
                    ReceivedUtc = request.ReceivedUtc,
                },
                CreditedAmount = request.CreditExcess ? Math.Max(0m, request.Amount - 60m) : 0m,
            });
    }

    public static TheoryData<string, string> Tools => new()
    {
        { ListTransactionsTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { GetTransactionTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { RecordTransactionPaymentTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { AdjustTransactionTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { CancelTransactionTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { SendTransactionNoticeTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { AddTransactionNoteTool.TheName, TransactionsPermissions.ManageTransactions.Name },
        { ListPaymentsTool.TheName, TransactionsPermissions.ManageRefunds.Name },
        { RefundPaymentTool.TheName, TransactionsPermissions.ManageRefunds.Name },
        { ListRefundsTool.TheName, TransactionsPermissions.ManageRefunds.Name },
        { ResolveRefundTool.TheName, TransactionsPermissions.ManageRefunds.Name },
        { ListPaymentIssuesTool.TheName, TransactionsPermissions.ManageRefunds.Name },
        { ResolvePaymentIssueTool.TheName, TransactionsPermissions.ManageRefunds.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), []);

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_transactions.Invocations);
        Assert.Empty(_attempts.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePagesPermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, []);

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_transactions.Invocations);
        Assert.Empty(_commands.Invocations);
        Assert.Empty(_refundService.Invocations);
    }

    [Fact]
    public async Task RecordTransactionPayment_NeedsEntireBalance_ForWhatIsStillOwed()
    {
        // Act
        var withoutFlag = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), Payment(60m, entireBalance: null, key: "pay-1"));
        var tooMuch = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), Payment(70m, entireBalance: null, key: "pay-2"));
        var otherCurrency = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(10m, null, "pay-3")) { ["currency"] = "EUR" });

        // Assert
        Assert.Equal("Nothing was recorded: 60.00 USD is everything still owed. Set entireBalance to true to record it as paid in full.", withoutFlag);
        Assert.Equal("Nothing was recorded: only 60.00 USD is still owed on this transaction. To keep what was paid above that as the customer's credit, set creditExcess and entireBalance to true.", tooMuch);
        Assert.Equal("Nothing was recorded: the transaction is in USD; give the amount in USD.", otherCurrency);
        Assert.Empty(_commands.Invocations);
    }

    [Fact]
    public async Task RecordTransactionPayment_RecordsOnce_PerIdempotencyKey()
    {
        // Arrange
        var services = Build();

        // Act
        var first = AgentToolTestContext.ReadJson(await InvokeAsync(RecordTransactionPaymentTool.TheName, services, Payment(25m, null, "pay-wire-8831")));
        var again = await InvokeAsync(RecordTransactionPaymentTool.TheName, services, Payment(25m, null, "pay-wire-8831"));

        // Assert
        Assert.Equal(25m, first.GetProperty("recorded").GetDecimal());
        Assert.StartsWith("Nothing was recorded again: the idempotency key 'pay-wire-8831' was already used", again, StringComparison.Ordinal);
        _commands.Verify(commands => commands.RecordPaymentAsync("tx-1", It.Is<TransactionPaymentRequest>(request => request.Amount == 25m && request.Reference == "WIRE-8831"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordTransactionPayment_RecordsHowAndWhenItWasReceived_EnteredInTheSitesTimeZone()
    {
        // Arrange
        var services = Build();
        var arguments = new Dictionary<string, object>(Payment(25m, null, "pay-check-1042"))
        {
            ["paymentMethod"] = "check",
            ["receivedAt"] = "2026-11-12T09:30",
        };

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RecordTransactionPaymentTool.TheName, services, arguments));
        var otherMethod = await InvokeAsync(RecordTransactionPaymentTool.TheName, services, new Dictionary<string, object>(arguments) { ["paymentMethod"] = "cash" });

        // Assert
        _commands.Verify(commands => commands.RecordPaymentAsync("tx-1", It.Is<TransactionPaymentRequest>(request =>
            request.Amount == 25m &&
            request.PaymentMethod == "check" &&
            request.ReceivedUtc == new DateTime(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc) &&
            !request.CreditExcess), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("check", json.GetProperty("paymentMethod").GetString());
        Assert.StartsWith("2026-11-12T09:30:00", json.GetProperty("receivedAt").GetString(), StringComparison.Ordinal);
        Assert.Equal("Nothing was recorded: the idempotency key 'pay-check-1042' was already used for something else. Use a new key.", otherMethod);
    }

    [Fact]
    public async Task RecordTransactionPayment_RefusesAReceivedTimeInTheFuture_OrNotATime_AndAPaymentMethodLongerThanTheForm()
    {
        // Act
        var future = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(25m, null, "pay-1")) { ["receivedAt"] = "2026-11-21T08:00" });
        var notATime = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(25m, null, "pay-2")) { ["receivedAt"] = "last Tuesday" });
        var longMethod = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(25m, null, "pay-3")) { ["paymentMethod"] = new string('x', RecordTransactionPaymentTool.MaxPaymentMethodLength + 1) });

        // Assert
        Assert.Equal("Nothing was recorded: the time the payment was received can't be in the future.", future);
        Assert.Equal("Nothing was recorded: give receivedAt as yyyy-MM-ddTHH:mm in the site's time zone.", notATime);
        Assert.Equal("Nothing was recorded: paymentMethod can be at most 100 characters.", longMethod);
        Assert.Empty(_commands.Invocations);
    }

    [Fact]
    public async Task RecordTransactionPayment_RecordsMoreThanIsOwed_OnlyWhenTheRestIsKeptAsCredit()
    {
        // Act
        var withoutEntireBalance = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(70m, null, "pay-1")) { ["creditExcess"] = true });
        var credited = AgentToolTestContext.ReadJson(await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(70m, entireBalance: true, "pay-2")) { ["creditExcess"] = true }));

        // Assert
        Assert.Equal("Nothing was recorded: creditExcess keeps what was paid above everything still owed as the customer's credit, so it needs entireBalance set to true too.", withoutEntireBalance);
        Assert.Equal(60m, credited.GetProperty("recorded").GetDecimal());
        Assert.Equal(10m, credited.GetProperty("creditedAmount").GetDecimal());
        Assert.Contains("10.00 USD above what was owed was added to the customer's credit.", credited.GetProperty("message").GetString(), StringComparison.Ordinal);
        _commands.Verify(commands => commands.RecordPaymentAsync("tx-1", It.Is<TransactionPaymentRequest>(request => request.Amount == 70m && request.CreditExcess), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordTransactionPayment_ReplaysARetriedKey_BeforeCheckingWhatIsStillOwed()
    {
        // Arrange
        var services = Build();
        var first = await InvokeAsync(RecordTransactionPaymentTool.TheName, services, Payment(60m, entireBalance: true, "pay-in-full-1"));

        // The payment settled the transaction: checked again, 60 USD would now be refused as more than is owed.
        _transaction.AmountPaid = 100m;

        // Act
        var retried = await InvokeAsync(RecordTransactionPaymentTool.TheName, services, Payment(60m, entireBalance: true, "pay-in-full-1"));

        // Assert
        Assert.StartsWith("{", first, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was recorded again: the idempotency key 'pay-in-full-1' was already used for this.", retried, StringComparison.Ordinal);
        Assert.DoesNotContain("creditExcess", retried, StringComparison.Ordinal);
        _commands.Verify(commands => commands.RecordPaymentAsync("tx-1", It.IsAny<TransactionPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordTransactionPayment_RefusesCreditExcessWithoutEntireBalance_AndAnAccountNumber()
    {
        // Act
        var creditExcessAlone = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(30m, null, "pay-1")) { ["creditExcess"] = true });
        var iban = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(30m, null, "pay-2")) { ["reference"] = "DE89 3704 0044 0532 0130 00" });
        var ordinary = await InvokeAsync(RecordTransactionPaymentTool.TheName, Build(), new Dictionary<string, object>(Payment(30m, null, "pay-3")) { ["reference"] = "Wire ref 2025-11-20-0001", ["note"] = "FY25 payment for March" });

        // Assert
        Assert.Equal("Nothing was recorded: creditExcess keeps what was paid above everything still owed as the customer's credit, so it needs entireBalance set to true too.", creditExcessAlone);
        Assert.Contains("card or bank account number", iban, StringComparison.Ordinal);
        Assert.StartsWith("{", ordinary, StringComparison.Ordinal);
        _commands.Verify(commands => commands.RecordPaymentAsync("tx-1", It.Is<TransactionPaymentRequest>(request => request.Reference == "Wire ref 2025-11-20-0001"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransaction_MarksTheTextPeopleWrote_AsData()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetTransactionTool.TheName, Build(), new() { ["transactionId"] = "tx-1" }));

        // Assert
        Assert.Equal(GetTransactionTool.DataNote, json.GetProperty("dataNote").GetString());
        Assert.Contains("lastFailure", GetTransactionTool.DataNote, StringComparison.Ordinal);
        Assert.Contains("message", GetTransactionTool.DataNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTransaction_ShowsThePayerTaxAndPartPayments_AndHowAndWhenEachPaymentByHandWasReceived()
    {
        // Arrange
        _transaction.SetPayer(new PartyRef(PartyKinds.Account, "account-7"));
        _transaction.PayerContact = ContactInfo.From("Northwind Traders", "ap@northwind.example", "+15550001111");
        _transaction.TaxLines = [new TaxLine { TaxName = "State tax", Rate = 0.0625m, TaxAmount = 6.25m }];
        _transaction.AllowPartialPayment = true;
        _transaction.MinimumPayment = 20m;
        _transaction.RemindersPaused = true;
        foreach (var entry in new[]
        {
            new TransactionEvent
            {
                Id = "event-1",
                Type = TransactionEventType.PaymentRecorded,
                Amount = 40m,
                CreatedUtc = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc),
                PaymentMethod = "check",
                ReceivedUtc = new DateTime(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc),
                Reference = "CHK-311",
            },
            new TransactionEvent
            {
                Id = "event-2",
                Type = TransactionEventType.Adjusted,
                Amount = 5m,
                CreatedUtc = new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc),
                DocumentKind = "CreditNote",
                DocumentNumber = "CN-1001",
            },
            new TransactionEvent
            {
                Id = "event-3",
                Type = TransactionEventType.Reversed,
                Amount = 5m,
                CreatedUtc = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc),
                ReversesEventId = "event-2",
            },
        })
        {
            _transaction.Events.Add(entry);
        }

        // Act
        var result = await InvokeAsync(GetTransactionTool.TheName, Build(), new() { ["transactionId"] = "tx-1" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var payer = json.GetProperty("payer");
        Assert.Equal(PartyKinds.Account, payer.GetProperty("kind").GetString());
        Assert.Equal("account-7", payer.GetProperty("id").GetString());
        Assert.Equal("Northwind Traders", payer.GetProperty("name").GetString());
        Assert.Equal("ap@northwind.example", payer.GetProperty("email").GetString());
        Assert.DoesNotContain("+1555", result, StringComparison.Ordinal);
        Assert.Equal(6.25m, Assert.Single(json.GetProperty("taxLines").EnumerateArray()).GetProperty("ratePercent").GetDecimal());
        Assert.True(json.GetProperty("allowPartialPayment").GetBoolean());
        Assert.Equal(20m, json.GetProperty("minimumPayment").GetDecimal());
        Assert.True(json.GetProperty("remindersPaused").GetBoolean());

        var timeline = json.GetProperty("timeline").EnumerateArray().ToDictionary(entry => entry.GetProperty("id").GetString());
        Assert.Equal("check", timeline["event-1"].GetProperty("paymentMethod").GetString());
        Assert.StartsWith("2026-11-12T09:30:00", timeline["event-1"].GetProperty("receivedAt").GetString(), StringComparison.Ordinal);
        Assert.Equal("CN-1001", timeline["event-2"].GetProperty("documentNumber").GetString());
        Assert.True(timeline["event-2"].GetProperty("takenBack").GetBoolean());
        Assert.Equal("event-2", timeline["event-3"].GetProperty("reversesEventId").GetString());
        Assert.False(timeline["event-3"].TryGetProperty("paymentMethod", out _));
    }

    [Fact]
    public async Task RefundPayment_RefusesAPaymentOfAnOrder()
    {
        // Act
        var result = await InvokeAsync(RefundPaymentTool.TheName, Build(), new()
        {
            ["paymentId"] = "attempt-1",
            ["amount"] = 10m,
            ["currency"] = "USD",
            ["idempotencyKey"] = "refund-1",
        });

        // Assert
        Assert.StartsWith("Nothing was refunded: this payment belongs to an order (id 'order-1'). Refund it with refundOrder", result, StringComparison.Ordinal);
        Assert.Empty(_refundService.Invocations);
    }

    [Fact]
    public async Task ResolveRefund_NeedsAReference()
    {
        // Act
        var result = await InvokeAsync(ResolveRefundTool.TheName, Build(), new() { ["refundId"] = "refund-1" });

        // Assert
        Assert.Equal("Nothing was settled: give the reference of the money returned by hand, such as the bank transfer number.", result);
        Assert.Empty(_refunds.Invocations);
    }

    private static Dictionary<string, object> Payment(decimal amount, bool? entireBalance, string key)
    {
        var arguments = new Dictionary<string, object>
        {
            ["transactionId"] = "tx-1",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["reference"] = "WIRE-8831",
            ["idempotencyKey"] = key,
        };

        if (entireBalance is { } flag)
        {
            arguments["entireBalance"] = flag;
        }

        return arguments;
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_transactions.Object);
        services.AddSingleton(_commands.Object);
        services.AddSingleton(_attempts.Object);
        services.AddSingleton(_refunds.Object);
        services.AddSingleton(_issues.Object);
        services.AddSingleton(_refundService.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        services.AddSingleton<IDistributedLock>(new FakeDistributedLock());
        services.AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == _now));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListTransactionsTool.TheName => new ListTransactionsTool(),
            GetTransactionTool.TheName => new GetTransactionTool(),
            RecordTransactionPaymentTool.TheName => new RecordTransactionPaymentTool(),
            AdjustTransactionTool.TheName => new AdjustTransactionTool(),
            CancelTransactionTool.TheName => new CancelTransactionTool(),
            SendTransactionNoticeTool.TheName => new SendTransactionNoticeTool(),
            AddTransactionNoteTool.TheName => new AddTransactionNoteTool(),
            ListPaymentsTool.TheName => new ListPaymentsTool(),
            RefundPaymentTool.TheName => new RefundPaymentTool(),
            ListRefundsTool.TheName => new ListRefundsTool(),
            ResolveRefundTool.TheName => new ResolveRefundTool(),
            ListPaymentIssuesTool.TheName => new ListPaymentIssuesTool(),
            _ => new ResolvePaymentIssueTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
