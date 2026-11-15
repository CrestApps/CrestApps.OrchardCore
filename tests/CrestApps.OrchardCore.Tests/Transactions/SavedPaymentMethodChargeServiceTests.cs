using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Invoicing;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The saved card charge service is the one charge-and-retry path for every module that collects a debt with a card:
/// invoices, installment plans and booking balances. A module that owns its card and retry schedule sets them on the
/// charge (the card a checkout kept, its own retry days); the service charges that card, retries on those days, tells
/// the module about each decline, and hands the debt over when no retry is left.
/// </summary>
public sealed class SavedPaymentMethodChargeServiceTests
{
    [Fact]
    public async Task AChargeWithItsOwnCardAndRetryDays_ChargesThatCard_RetriesOnThoseDays_AndReportsEachDecline()
    {
        // Arrange: a debt whose module set the charge itself: the card its checkout kept, retried once two days later.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var transactionId = await SeedDebtAsync(harness, new TransactionAutoCharge
        {
            SourceSessionId = "deposit-session",
            RetryDays = [2],
            NextAttemptUtc = InvoicingTestHarness.Now,
        });

        harness.ChargeOutcomes.Enqueue(SavedPaymentMethodChargeStatus.Declined);
        harness.ChargeOutcomes.Enqueue(SavedPaymentMethodChargeStatus.Declined);

        // Act
        var first = await ChargeAsync(harness, transactionId);
        var afterFirst = await harness.FindTransactionAsync(transactionId);
        var second = await ChargeAsync(harness, transactionId);
        var afterSecond = await harness.FindTransactionAsync(transactionId);

        // Assert
        Assert.Equal(["kept:deposit-session", "kept:deposit-session"], harness.ChargedCards);
        Assert.Equal((false, InvoicingTestHarness.Now.AddDays(2)), (first.HandedOver, first.NextAttemptUtc));
        Assert.Equal((1, InvoicingTestHarness.Now.AddDays(2)), (afterFirst.AutoCharge.Attempts, afterFirst.AutoCharge.NextAttemptUtc));

        var declined = Assert.Single(harness.DeclinedCharges);
        Assert.Equal((transactionId, InvoicingTestHarness.Now.AddDays(2)), (declined.TransactionId, declined.NextAttemptUtc));

        Assert.True(second.HandedOver);
        Assert.Null(afterSecond.AutoCharge);
    }

    [Fact]
    public async Task AChargeWithNoRetryDays_IsHandedOverAtTheFirstDecline_WithoutADeclinedEvent()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var transactionId = await SeedDebtAsync(harness, new TransactionAutoCharge
        {
            SourceSessionId = "deposit-session",
            RetryDays = [],
            NextAttemptUtc = InvoicingTestHarness.Now,
        });

        harness.ChargeOutcomes.Enqueue(SavedPaymentMethodChargeStatus.Declined);

        // Act
        var result = await ChargeAsync(harness, transactionId);

        // Assert
        Assert.True(result.HandedOver);
        Assert.Empty(harness.DeclinedCharges);
    }

    [Fact]
    public async Task AChargeSetOnTheTransaction_IsChargedByTheSweepOnItsDay()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var transactionId = await SeedDebtAsync(harness, new TransactionAutoCharge
        {
            SourceSessionId = "deposit-session",
            RetryDays = [1, 1],
            NextAttemptUtc = InvoicingTestHarness.Now.AddDays(3),
        });

        // Act
        var early = await SweepAsync(harness, InvoicingTestHarness.Now.AddDays(1));
        var onTime = await SweepAsync(harness, InvoicingTestHarness.Now.AddDays(3).AddHours(1));

        // Assert
        Assert.Equal((0, 1), (early, onTime));
        Assert.Equal(TransactionStatus.Paid, (await harness.FindTransactionAsync(transactionId)).Status);
    }

    // An issued invoice's debt, given the charge a module sets on the transaction it owns.
    private static async Task<string> SeedDebtAsync(InvoicingTestHarness harness, TransactionAutoCharge charge)
    {
        var draft = await harness.CreateDraftAsync(invoice => invoice.BillTo = new DocumentParty
        {
            Party = new PartyRef(PartyKinds.User, "user-1"),
            Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" },
        });
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ITransactionManager>();
            var transaction = await manager.FindByIdAsync(issued.Invoice.TransactionId, TestContext.Current.CancellationToken);

            transaction.AutoCharge = charge;
            await manager.UpdateAsync(transaction, data: null, TestContext.Current.CancellationToken);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

            return true;
        });

        return issued.Invoice.TransactionId;
    }

    private static Task<SavedPaymentMethodChargeResult> ChargeAsync(InvoicingTestHarness harness, string transactionId)
        => harness.InScopeAsync(services => services.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeAsync(transactionId, TestContext.Current.CancellationToken));

    private static Task<int> SweepAsync(InvoicingTestHarness harness, DateTime utcNow)
        => harness.InScopeAsync(services => services.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeDueAsync(utcNow, TestContext.Current.CancellationToken));
}
