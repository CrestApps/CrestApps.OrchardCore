using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The management screens each load, mutate, and save a transaction. These rules are what stop an operator from
/// recording money against an obligation that is no longer collectable, which would corrupt a ledger whose only
/// job is to answer "what is still owed".
/// </summary>
public class TransactionStateMachineTests
{
    [Theory]
    [InlineData(TransactionStatus.Pending)]
    [InlineData(TransactionStatus.Outstanding)]
    [InlineData(TransactionStatus.PartiallyPaid)]
    [InlineData(TransactionStatus.Failed)]
    public void CollectableStatuses_AllowManagement(TransactionStatus status)
    {
        var transaction = CreateTransaction(status, totalAmount: 100m, amountPaid: 0m);

        Assert.True(TransactionStateMachine.IsCollectable(status));
        Assert.True(TransactionStateMachine.CanRecordPayment(transaction));
        Assert.True(TransactionStateMachine.CanMarkPaid(transaction));
        Assert.True(TransactionStateMachine.CanCancel(transaction));
        Assert.True(TransactionStateMachine.CanSendReminder(transaction));
    }

    [Theory]
    [InlineData(TransactionStatus.Paid)]
    [InlineData(TransactionStatus.Canceled)]
    [InlineData(TransactionStatus.Refunded)]
    [InlineData(TransactionStatus.Abandoned)]
    public void TerminalStatuses_RefuseManagement(TransactionStatus status)
    {
        var transaction = CreateTransaction(status, totalAmount: 100m, amountPaid: 0m);

        Assert.False(TransactionStateMachine.IsCollectable(status));
        Assert.False(TransactionStateMachine.CanRecordPayment(transaction));
        Assert.False(TransactionStateMachine.CanMarkPaid(transaction));
        Assert.False(TransactionStateMachine.CanCancel(transaction));
        Assert.False(TransactionStateMachine.CanSendReminder(transaction));
    }

    /// <summary>
    /// A transaction whose balance is already covered has nothing left to collect, even while its status still
    /// says outstanding — for example between a settlement being applied and the status being recomputed.
    /// </summary>
    [Fact]
    public void FullyCoveredBalance_RefusesPaymentAndReminder()
    {
        var transaction = CreateTransaction(TransactionStatus.Outstanding, totalAmount: 100m, amountPaid: 100m);

        Assert.False(TransactionStateMachine.CanRecordPayment(transaction));
        Assert.False(TransactionStateMachine.CanSendReminder(transaction));

        // Settling and canceling remain legal: they are status corrections, not money movements.
        Assert.True(TransactionStateMachine.CanMarkPaid(transaction));
        Assert.True(TransactionStateMachine.CanCancel(transaction));
    }

    [Fact]
    public void NullTransaction_IsNeverManageable()
    {
        Assert.False(TransactionStateMachine.CanRecordPayment(null));
        Assert.False(TransactionStateMachine.CanMarkPaid(null));
        Assert.False(TransactionStateMachine.CanCancel(null));
        Assert.False(TransactionStateMachine.CanSendReminder(null));
    }

    private static Transaction CreateTransaction(TransactionStatus status, decimal totalAmount, decimal amountPaid)
        => new()
        {
            ItemId = "transaction-1",
            Currency = "USD",
            Status = status,
            TotalAmount = totalAmount,
            AmountPaid = amountPaid,
        };
}
