using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An invoice marked to be charged automatically is charged to the payer's default saved card when it is issued (or on
/// its due date), retried when the card is declined, and sent to the payer to pay when no card can be charged or the
/// retries run out.
/// </summary>
public sealed class InvoiceAutoChargeTests
{
    private const string UserId = "user-7";

    private static readonly CustomerPaymentMethod _card = new()
    {
        ItemId = "card-1",
        OwnerId = UserId,
        PaymentMethod = new SavedPaymentMethod { ProviderKey = "Stripe", Brand = "Visa", Last4 = "4242", ExpirationMonth = 12, ExpirationYear = 2030 },
        IsDefault = true,
    };

    [Fact]
    public async Task AnInvoiceDueOnIssue_IsChargedToTheDefaultCard_AndSettled()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        harness.SavedCards.Setup(store => store.GetDefaultAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_card);

        // Act
        var issued = await IssueAsync(harness);

        // Assert
        var transaction = await harness.FindTransactionAsync(issued.TransactionId);
        Assert.Equal(["card-1"], harness.ChargedCards);
        Assert.Equal((0m, TransactionStatus.Paid), (transaction.OutstandingAmount, transaction.Status));
        Assert.Null(transaction.AutoCharge);
        Assert.Empty(harness.Emails);
    }

    [Fact]
    public async Task ADeclinedCard_IsRetried_ThenTheInvoiceIsSentToBePaid()
    {
        // Arrange: four declines, the first charge and the three retries of the default schedule (1, 3 and 5 days).
        await using var harness = await InvoicingTestHarness.CreateAsync();
        harness.SavedCards.Setup(store => store.GetDefaultAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_card);

        for (var i = 0; i < 4; i++)
        {
            harness.ChargeOutcomes.Enqueue(SavedPaymentMethodChargeStatus.Declined);
        }

        // Act
        var issued = await IssueAsync(harness);
        var afterFirst = await harness.FindTransactionAsync(issued.TransactionId);
        var attemptsAfterFirst = afterFirst.AutoCharge?.Attempts;
        var retryAfterFirst = afterFirst.AutoCharge?.NextAttemptUtc;

        var outcomes = new List<SavedPaymentMethodChargeResult>();

        for (var retry = 0; retry < 3; retry++)
        {
            outcomes.Add(await ChargeAsync(harness, issued.TransactionId));
        }

        // Assert
        Assert.Equal((1, InvoicingTestHarness.Now.AddDays(1)), (attemptsAfterFirst, retryAfterFirst));
        Assert.Equal([false, false, true], outcomes.Select(outcome => outcome.HandedOver));

        var transaction = await harness.FindTransactionAsync(issued.TransactionId);
        Assert.Null(transaction.AutoCharge);
        Assert.Null(transaction.AutoCollection);
        Assert.Equal(100m, transaction.OutstandingAmount);

        var email = Assert.Single(harness.Emails);
        Assert.Contains("ap@fabrikam.example", email.Email.To);
    }

    [Fact]
    public async Task APayerWithoutASavedCard_IsSentTheInvoice()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var issued = await IssueAsync(harness);

        // Assert
        var transaction = await harness.FindTransactionAsync(issued.TransactionId);
        Assert.Empty(harness.ChargedCards);
        Assert.Null(transaction.AutoCharge);
        Assert.Single(harness.Emails);
    }

    [Fact]
    public async Task AnInvoiceDueLater_IsChargedOnItsDueDate_ByTheSweep()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        harness.SavedCards.Setup(store => store.GetDefaultAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_card);

        var issued = await IssueAsync(harness, draft => draft.DueDate = DateOnly.FromDateTime(InvoicingTestHarness.Now).AddDays(10));
        var scheduled = await harness.FindTransactionAsync(issued.TransactionId);

        // Act
        var early = await SweepAsync(harness, InvoicingTestHarness.Now.AddDays(5));
        var onTime = await SweepAsync(harness, scheduled.AutoCharge.NextAttemptUtc.AddHours(1));

        // Assert
        Assert.Equal((new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc), "Visa •••• 4242"), (scheduled.AutoCharge.NextAttemptUtc, scheduled.AutoCollection.PaymentMethodDescription));
        Assert.Equal((0, 1), (early, onTime));
        Assert.Equal(TransactionStatus.Paid, (await harness.FindTransactionAsync(issued.TransactionId)).Status);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, Action<Invoice> configure = null)
    {
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.AutoCharge = true;
            invoice.BillTo = new DocumentParty
            {
                Party = new PartyRef(PartyKinds.User, UserId),
                Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" },
            };
            configure?.Invoke(invoice);
        });

        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static Task<SavedPaymentMethodChargeResult> ChargeAsync(InvoicingTestHarness harness, string transactionId)
        => harness.InScopeAsync(services => services.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeAsync(transactionId, TestContext.Current.CancellationToken));

    private static Task<int> SweepAsync(InvoicingTestHarness harness, DateTime utcNow)
        => harness.InScopeAsync(services => services.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeDueAsync(utcNow, TestContext.Current.CancellationToken));
}
