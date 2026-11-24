using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>adjustTransaction</c> lowers what is owed only with the token <c>previewTransactionAction</c> issued for the same
/// adjustment and the same state of the transaction, gives a retry with the same key its first answer back even though
/// the balance has changed since, refuses a reason that holds a card number, and asks for approval.
/// </summary>
public sealed class AdjustTransactionToolTests
{
    private readonly TransactionActionToolContext _context = new();

    public AdjustTransactionToolTests()
    {
        _context.Commands
            .Setup(commands => commands.AdjustAsync("tx-1", It.IsAny<decimal>(), It.IsAny<TransactionAdjustmentKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, decimal amount, TransactionAdjustmentKind _, string _, CancellationToken _) => new TransactionCommandResult
            {
                Transaction = _context.Transaction,
                Event = new TransactionEvent { Id = "adjustment-1", Type = TransactionEventType.Adjusted, Amount = amount },
            });
    }

    [Fact]
    public async Task AdjustTransaction_WritesOffEverythingOwed_WithThePreviewsToken_AndReplaysARetriedKey()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.Adjust(50m, entireBalance: true));

        // Act
        var first = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new AdjustTransactionTool(), services, Arguments(token, "writeoff-1")));

        // The write-off settled the transaction: checked again, 50 USD would be refused as more than is owed.
        _context.Transaction.AmountAdjusted = 60m;
        var again = await TransactionActionToolContext.InvokeAsync(new AdjustTransactionTool(), services, Arguments(token, "writeoff-1"));

        // Assert
        Assert.Equal(50m, first.GetProperty("adjustedBy").GetDecimal());
        Assert.Equal("writeOff", first.GetProperty("kind").GetString());
        Assert.StartsWith("Nothing was adjusted again: the idempotency key 'writeoff-1' was already used", again, StringComparison.Ordinal);
        _context.Commands.Verify(commands => commands.AdjustAsync("tx-1", 50m, TransactionAdjustmentKind.WriteOff, "Customer went out of business", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustTransaction_NeedsTheTokenOfTheSameAdjustment()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.Adjust(20m, kind: "discount"));

        // Act
        var missing = await TransactionActionToolContext.InvokeAsync(new AdjustTransactionTool(), services, Arguments(null, "key-1", amount: 20m, kind: "discount", entireBalance: false));
        var otherKind = await TransactionActionToolContext.InvokeAsync(new AdjustTransactionTool(), services, Arguments(token, "key-2", amount: 20m, kind: "writeOff", entireBalance: false));
        var withCard = Arguments(token, "key-3", amount: 20m, kind: "discount", entireBalance: false);
        withCard["reason"] = "Refunded to 4111-1111-1111-1111";
        var card = await TransactionActionToolContext.InvokeAsync(new AdjustTransactionTool(), services, withCard);

        // Assert
        Assert.StartsWith("Nothing was adjusted: call previewTransactionAction first", missing, StringComparison.Ordinal);
        Assert.Contains("issued for other arguments", otherKind, StringComparison.Ordinal);
        Assert.Contains("card or bank account number", card, StringComparison.Ordinal);
        Assert.Empty(_context.Commands.Invocations);
    }

    [Fact]
    public void AdjustTransaction_AsksForApproval()
    {
        // Assert
        Assert.True(TransactionActionToolContext.RequiresApproval(AdjustTransactionTool.TheName));
    }

    private static Dictionary<string, object> Arguments(string token, string key, decimal amount = 50m, string kind = "writeOff", bool entireBalance = true)
    {
        var arguments = new Dictionary<string, object>
        {
            ["transactionId"] = "tx-1",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["kind"] = kind,
            ["entireBalance"] = entireBalance,
            ["reason"] = "Customer went out of business",
            ["idempotencyKey"] = key,
        };

        if (token is not null)
        {
            arguments["previewToken"] = token;
        }

        return arguments;
    }
}
