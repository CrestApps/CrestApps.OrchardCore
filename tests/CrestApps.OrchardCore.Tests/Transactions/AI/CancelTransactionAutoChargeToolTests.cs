using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>cancelTransactionAutoCharge</c> refuses a caller who is not signed in or lacks Manage transactions, cancels the
/// scheduled card charge through the charge service and returns the charge it canceled in local time, says so when no
/// charge is scheduled, relays the service's refusals, and asks for approval because no admin page can schedule the
/// charge again.
/// </summary>
public sealed class CancelTransactionAutoChargeToolTests
{
    private readonly TransactionActionToolContext _context = new();

    [Fact]
    public async Task CancelTransactionAutoCharge_RefusesACallerWhoIsNotSignedIn_OrWithoutManageTransactions()
    {
        // Arrange
        var services = _context.Build(authorize: (asked, _) => asked.Name != TransactionsPermissions.ManageTransactions.Name);

        // Act
        var anonymous = await TransactionActionToolContext.InvokeAsync(new CancelTransactionAutoChargeTool(), _context.Build(signedIn: false), Arguments());
        var refused = await TransactionActionToolContext.InvokeAsync(new CancelTransactionAutoChargeTool(), services, Arguments());

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_context.Charges.Invocations);
    }

    [Fact]
    public async Task CancelTransactionAutoCharge_SaysSo_WhenNoChargeIsScheduled()
    {
        // Act
        var answer = await TransactionActionToolContext.InvokeAsync(new CancelTransactionAutoChargeTool(), _context.Build(), Arguments());

        // Assert
        Assert.Equal("Nothing was canceled: no card charge is scheduled on this transaction.", answer);
        Assert.Empty(_context.Charges.Invocations);
    }

    [Fact]
    public async Task CancelTransactionAutoCharge_CancelsTheCharge_AndReturnsItInLocalTime()
    {
        // Arrange
        Schedule();
        _context.Charges
            .Setup(charges => charges.CancelAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _context.Transaction.AutoCharge = null;
                _context.Transaction.AutoCollection = null;

                return new SavedPaymentMethodChargeResult { Transaction = _context.Transaction };
            });

        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new CancelTransactionAutoChargeTool(), _context.Build(), Arguments()));

        // Assert
        var canceled = answer.GetProperty("canceledCharge");
        Assert.Equal("Visa ending 4242", canceled.GetProperty("paymentMethod").GetString());
        Assert.StartsWith("2026-11-23T12:00:00", canceled.GetProperty("nextAttemptAt").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, canceled.GetProperty("declinedSoFar").GetInt32());
        _context.Charges.Verify(charges => charges.CancelAsync("tx-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelTransactionAutoCharge_RelaysTheServicesRefusal()
    {
        // Arrange
        Schedule();
        _context.Charges
            .Setup(charges => charges.CancelAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SavedPaymentMethodChargeResult().Fail("This transaction is already being charged. Try again in a moment."));

        // Act
        var answer = await TransactionActionToolContext.InvokeAsync(new CancelTransactionAutoChargeTool(), _context.Build(), Arguments());

        // Assert
        Assert.Equal("Nothing was canceled: This transaction is already being charged. Try again in a moment.", answer);
    }

    [Fact]
    public void CancelTransactionAutoCharge_AsksForApproval()
    {
        // Assert
        Assert.True(TransactionActionToolContext.RequiresApproval(CancelTransactionAutoChargeTool.TheName));
    }

    private void Schedule()
    {
        _context.Transaction.AutoCharge = new TransactionAutoCharge
        {
            SavedPaymentMethodId = "pm-1",
            NextAttemptUtc = TransactionActionToolContext.Now.AddDays(3),
            Attempts = 1,
            LastFailure = "Insufficient funds",
        };
        _context.Transaction.AutoCollection = new TransactionAutoCollection { PaymentMethodDescription = "Visa ending 4242" };
    }

    private static Dictionary<string, object> Arguments()
        => new() { ["transactionId"] = "tx-1" };
}
