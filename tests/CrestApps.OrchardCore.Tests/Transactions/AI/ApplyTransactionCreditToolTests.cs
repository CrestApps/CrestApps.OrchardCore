using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>applyTransactionCredit</c> refuses a caller who is not signed in or lacks Manage transactions, pays with the
/// payer's customer credit only with the preview's token and an idempotency key, gives a retry with the same key its first
/// answer back even though the balance has changed since, keeps the key free when the payment is refused, relays the
/// warning of a credit taken back, is offered only with Store Credit, and asks for approval.
/// </summary>
public sealed class ApplyTransactionCreditToolTests
{
    private readonly TransactionActionToolContext _context = new();

    public ApplyTransactionCreditToolTests()
    {
        _context.Commands
            .Setup(commands => commands.ApplyCreditAsync("tx-1", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, decimal amount, string _, CancellationToken _) => new TransactionCommandResult
            {
                Transaction = _context.Transaction,
                Event = new TransactionEvent { Id = "credit-1", Type = TransactionEventType.PaymentRecorded, Amount = amount },
            });
    }

    [Fact]
    public async Task ApplyTransactionCredit_RefusesACallerWhoIsNotSignedIn_OrWithoutManageTransactions()
    {
        // Arrange
        var services = _context.Build(authorize: (asked, _) => asked.Name != TransactionsPermissions.ManageTransactions.Name);

        // Act
        var anonymous = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), _context.Build(signedIn: false), Arguments("token", "key-1"));
        var refused = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments("token", "key-1"));

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_context.Commands.Invocations);
    }

    [Fact]
    public async Task ApplyTransactionCredit_PaysWithTheCredit_AndAReplayedKeyGetsItsFirstAnswer_AfterTheBalanceChanged()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.ApplyCredit(20m));

        // Act
        var first = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments(token, "credit-inv42-1")));

        // The payment lowered what is owed and the credit: the checks would now refuse 20 USD.
        _context.Transaction.AmountPaid = 60m;
        _context.CreditBalance = 5m;
        var again = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments(token, "credit-inv42-1"));

        // Assert
        Assert.Equal(20m, first.GetProperty("applied").GetDecimal());
        Assert.StartsWith("Nothing was applied again: the idempotency key 'credit-inv42-1' was already used", again, StringComparison.Ordinal);
        _context.Commands.Verify(commands => commands.ApplyCreditAsync("tx-1", 20m, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyTransactionCredit_NeedsAKeyAndAToken_AndARefusalKeepsTheKeyFree()
    {
        // Arrange
        var services = _context.Build();
        var noKey = Arguments("token", null);
        noKey.Remove("idempotencyKey");

        // Act
        var keyless = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, noKey);
        var tokenless = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments(null, "key-1"));
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.ApplyCredit(20m));
        var applied = await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments(token, "key-1"));

        // Assert
        Assert.StartsWith("Nothing was applied: give a new idempotencyKey", keyless, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was applied: call previewTransactionAction first", tokenless, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing was applied", applied, StringComparison.Ordinal);
        _context.Commands.Verify(commands => commands.ApplyCreditAsync("tx-1", 20m, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyTransactionCredit_RelaysTheWarning_WhenTheCreditWasSpentMeanwhile()
    {
        // Arrange
        var result = new TransactionCommandResult { Transaction = _context.Transaction, Event = new TransactionEvent { Amount = 20m } };
        result.Warnings.Add("A payment with customer credit was taken back: the customer has only 0.00 USD of credit.");
        _context.Commands.Setup(commands => commands.ApplyCreditAsync("tx-1", 20m, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.ApplyCredit(20m));

        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new ApplyTransactionCreditTool(), services, Arguments(token, "key-1")));

        // Assert
        Assert.Contains("taken back", Assert.Single(answer.GetProperty("warnings").EnumerateArray()).GetString(), StringComparison.Ordinal);
        Assert.Contains("read the warnings", answer.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTransactionCredit_AsksForApproval_AndIsOfferedOnlyWithStoreCredit()
    {
        // Act
        var required = typeof(TransactionCreditAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

        // Assert
        Assert.True(TransactionActionToolContext.RequiresApproval(ApplyTransactionCreditTool.TheName));
        Assert.Contains(CustomersConstants.Features.StoreCredit, required.RequiredFeatureNames);
    }

    private static Dictionary<string, object> Arguments(string token, string key)
    {
        var arguments = new Dictionary<string, object>
        {
            ["transactionId"] = "tx-1",
            ["amount"] = 20m,
            ["currency"] = "USD",
            ["entireBalance"] = false,
            ["idempotencyKey"] = key,
        };

        if (token is not null)
        {
            arguments["previewToken"] = token;
        }

        return arguments;
    }
}
