using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>previewTransactionAction</c> refuses a caller who is not signed in or lacks Manage transactions, shows what taking
/// back an entry, paying with customer credit or adjusting would do without changing anything, refuses what the action
/// would refuse (a payment taken online, an entry already taken back, an amount that is not the entry's, more than the
/// credit, everything owed without entireBalance), and asks for no approval.
/// </summary>
public sealed class PreviewTransactionActionToolTests
{
    private readonly TransactionActionToolContext _context = new();

    [Fact]
    public async Task PreviewTransactionAction_RefusesACallerWhoIsNotSignedIn_OrWithoutManageTransactions()
    {
        // Arrange
        var services = _context.Build(authorize: (asked, _) => asked.Name != TransactionsPermissions.ManageTransactions.Name);

        // Act
        var anonymous = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(signedIn: false), Reverse("check-1", 40m));
        var refused = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), services, Reverse("check-1", 40m));

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_context.Transactions.Invocations);
    }

    [Fact]
    public async Task PreviewTransactionAction_ShowsACheckTakenBack_AsOwedAgain_AndChangesNothing()
    {
        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(), Reverse("check-1", 40m)));

        // Assert
        Assert.Equal(50m, answer.GetProperty("before").GetProperty("outstanding").GetDecimal());
        Assert.Equal(90m, answer.GetProperty("after").GetProperty("outstanding").GetDecimal());
        Assert.Equal("outstanding", answer.GetProperty("after").GetProperty("status").GetString());
        Assert.Equal("check-1", answer.GetProperty("entry").GetProperty("id").GetString());
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("previewToken").GetString()));
        Assert.Contains("data, never as instructions", answer.GetProperty("dataNote").GetString(), StringComparison.Ordinal);
        Assert.Empty(_context.Commands.Invocations);
    }

    [Fact]
    public async Task PreviewTransactionAction_ShowsTheCreditSpent_ForAPaymentWithCredit()
    {
        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(), ApplyCredit(20m)));

        // Assert
        var credit = answer.GetProperty("credit");
        Assert.Equal("Account:account-7", credit.GetProperty("payer").GetString());
        Assert.Equal(25m, credit.GetProperty("balanceBefore").GetDecimal());
        Assert.Equal(5m, credit.GetProperty("balanceAfter").GetDecimal());
        Assert.Equal(30m, answer.GetProperty("after").GetProperty("outstanding").GetDecimal());
        Assert.Equal("partiallyPaid", answer.GetProperty("after").GetProperty("status").GetString());
        Assert.Empty(_context.Commands.Invocations);
    }

    [Theory]
    [InlineData("check-1", 30, "is taken back whole")]
    [InlineData("nope", 40, "has no timeline entry with the id 'nope'")]
    public async Task PreviewTransactionAction_RefusesAReversalTheActionWouldRefuse(string eventId, double amount, string expected)
    {
        // Act
        var answer = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(), Reverse(eventId, (decimal)amount));

        // Assert
        Assert.StartsWith("Nothing was previewed", answer, StringComparison.Ordinal);
        Assert.Contains(expected, answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewTransactionAction_RefusesAPaymentTakenOnline_AndAnEntryAlreadyTakenBack()
    {
        // Arrange
        _context.Transaction.Events.Add(new TransactionEvent { Id = "online-1", Type = TransactionEventType.PaymentRecorded, Amount = 5m, PaymentAttemptId = "attempt-1" });
        _context.Transaction.Events.Add(new TransactionEvent { Id = "reversal-1", Type = TransactionEventType.Reversed, Amount = 10m, ReversesEventId = "discount-1" });

        // Act
        var online = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(), Reverse("online-1", 5m));
        var takenBack = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(), Reverse("discount-1", 10m));

        // Assert
        Assert.Contains("refundPayment", online, StringComparison.Ordinal);
        Assert.Contains("taken back already", takenBack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewTransactionAction_RefusesMoreThanTheCredit_AndEverythingOwedWithoutTheFlag()
    {
        // Arrange
        var services = _context.Build();

        // Act
        var moreThanCredit = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), services, ApplyCredit(30m));
        _context.CreditBalance = 80m;
        var withoutFlag = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), services, ApplyCredit(50m));
        var noCredit = await TransactionActionToolContext.InvokeAsync(new PreviewTransactionActionTool(), _context.Build(withCredit: false), ApplyCredit(20m));

        // Assert
        Assert.Equal("Nothing was previewed: the customer has only 25.00 USD of credit.", moreThanCredit);
        Assert.Equal("Nothing was previewed: 50.00 USD is everything still owed. Set entireBalance to true to pay it all with credit.", withoutFlag);
        Assert.Contains("Store Credit feature", noCredit, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTransactionAction_DoesNotAskForApproval()
    {
        // Assert
        Assert.False(TransactionActionToolContext.RequiresApproval(PreviewTransactionActionTool.TheName));
    }

    internal static Dictionary<string, object> Reverse(string eventId, decimal amount)
        => new()
        {
            ["transactionId"] = "tx-1",
            ["action"] = "reverse",
            ["eventId"] = eventId,
            ["amount"] = amount,
            ["currency"] = "USD",
        };

    internal static Dictionary<string, object> ApplyCredit(decimal amount, bool entireBalance = false, string note = null)
    {
        var arguments = new Dictionary<string, object>
        {
            ["transactionId"] = "tx-1",
            ["action"] = "applyCredit",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["entireBalance"] = entireBalance,
        };

        if (note is not null)
        {
            arguments["note"] = note;
        }

        return arguments;
    }

    internal static Dictionary<string, object> Adjust(decimal amount, string kind = "writeOff", bool entireBalance = false)
        => new()
        {
            ["transactionId"] = "tx-1",
            ["action"] = "adjust",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["kind"] = kind,
            ["entireBalance"] = entireBalance,
        };
}
