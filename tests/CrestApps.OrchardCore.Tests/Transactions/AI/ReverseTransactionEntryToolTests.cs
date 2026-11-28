using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>reverseTransactionEntry</c> refuses a caller who is not signed in or lacks Manage transactions, takes an entry
/// back only with the token <c>previewTransactionAction</c> issued for the same entry and the same state of the
/// transaction, uses a token once, refuses a reason that holds a card number, and asks for approval.
/// </summary>
public sealed class ReverseTransactionEntryToolTests
{
    private readonly TransactionActionToolContext _context = new();

    public ReverseTransactionEntryToolTests()
    {
        _context.Commands
            .Setup(commands => commands.ReverseAsync("tx-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TransactionCommandResult
            {
                Transaction = _context.Transaction,
                Event = new TransactionEvent { Id = "reversal-1", Type = TransactionEventType.Reversed, Amount = 40m, ReversesEventId = "check-1" },
            });
    }

    [Fact]
    public async Task ReverseTransactionEntry_RefusesACallerWhoIsNotSignedIn_OrWithoutManageTransactions()
    {
        // Arrange
        var services = _context.Build(authorize: (asked, _) => asked.Name != TransactionsPermissions.ManageTransactions.Name);

        // Act
        var anonymous = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), _context.Build(signedIn: false), Arguments("token"));
        var refused = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, Arguments("token"));

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_context.Commands.Invocations);
    }

    [Fact]
    public async Task ReverseTransactionEntry_TakesTheEntryBack_WithThePreviewsToken_Once()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.Reverse("check-1", 40m));

        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, Arguments(token)));
        var again = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, Arguments(token));

        // Assert
        Assert.Equal("reversal-1", answer.GetProperty("reversalId").GetString());
        Assert.Equal(40m, answer.GetProperty("takenBack").GetDecimal());
        Assert.StartsWith("Nothing was taken back: the previewToken is not one previewTransactionAction issued to you", again, StringComparison.Ordinal);
        _context.Commands.Verify(commands => commands.ReverseAsync("tx-1", "check-1", "Check returned unpaid", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReverseTransactionEntry_NeedsAToken_AndRefusesOneIssuedBeforeTheTransactionChanged()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.Reverse("check-1", 40m));
        var withoutToken = Arguments(token);
        withoutToken.Remove("previewToken");

        // Act
        var missing = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, withoutToken);
        _context.Transaction.AmountAdjusted = 20m;
        var changed = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, Arguments(token));

        // Assert
        Assert.StartsWith("Nothing was taken back: call previewTransactionAction first", missing, StringComparison.Ordinal);
        Assert.Contains("the transaction changed since the preview", changed, StringComparison.Ordinal);
        Assert.Empty(_context.Commands.Invocations);
    }

    [Fact]
    public async Task ReverseTransactionEntry_RefusesACardNumberInTheReason_AndKeepsTheToken()
    {
        // Arrange
        var services = _context.Build();
        var token = await TransactionActionToolContext.PreviewAsync(services, PreviewTransactionActionToolTests.Reverse("check-1", 40m));
        var withCard = Arguments(token);
        withCard["reason"] = "Card 4111 1111 1111 1111 was declined";

        // Act
        var card = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, withCard);
        var valid = await TransactionActionToolContext.InvokeAsync(new ReverseTransactionEntryTool(), services, Arguments(token));

        // Assert
        Assert.Contains("card or bank account number", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing was taken back", valid, StringComparison.Ordinal);
        _context.Commands.Verify(commands => commands.ReverseAsync("tx-1", "check-1", "Check returned unpaid", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ReverseTransactionEntry_AsksForApproval()
    {
        // Assert
        Assert.True(TransactionActionToolContext.RequiresApproval(ReverseTransactionEntryTool.TheName));
    }

    private static Dictionary<string, object> Arguments(string token)
        => new()
        {
            ["transactionId"] = "tx-1",
            ["eventId"] = "check-1",
            ["amount"] = 40m,
            ["currency"] = "USD",
            ["reason"] = "Check returned unpaid",
            ["previewToken"] = token,
        };
}
