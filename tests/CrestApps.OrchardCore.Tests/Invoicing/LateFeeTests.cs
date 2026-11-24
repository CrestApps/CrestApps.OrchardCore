using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A late fee is its own debit note linked to the overdue invoice, charged once its grace days have passed, once per
/// policy step (never twice), repeating up to the policy's cap, and never within the grace days, below the minimum
/// balance, on an exempt invoice or on one paid meanwhile.
/// </summary>
public sealed class LateFeeTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    [Fact]
    public async Task AnOverdueInvoice_PastItsGrace_GetsOneDebitNote_OnlyOnce()
    {
        // Arrange: $25 after five days of grace; the invoice is ten days late.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await AddPolicyAsync(harness, policy =>
        {
            policy.Kind = LateFeeKind.Flat;
            policy.Amount = 25m;
            policy.GraceDays = 5;
        });
        var invoice = await IssueOverdueAsync(harness, daysLate: 10);

        // Act
        var first = await SweepAsync(harness);
        var second = await SweepAsync(harness);

        // Assert
        Assert.Equal((1, 0), (first, second));

        var debitNote = Assert.Single(await DebitNotesAsync(harness, invoice.ItemId));
        Assert.Equal((InvoiceKind.DebitNote, InvoiceStatus.Open, invoice.ItemId, invoice.Number), (debitNote.Kind, debitNote.Status, debitNote.RelatedInvoiceId, debitNote.Reference));
        Assert.False(string.IsNullOrEmpty(debitNote.Number));
        Assert.Equal(25m, debitNote.Totals.Total);
        Assert.Equal((DocumentLineKind.Fee, TaxTreatment.OutOfScope), (debitNote.Lines[0].Kind, debitNote.Lines[0].TaxTreatment));
        Assert.Equal(25m, (await harness.FindTransactionAsync(debitNote.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task APercentFee_Repeats_ThenStopsAtItsCap()
    {
        // Arrange: 10% of what is due every 30 days, capped at $25; the $100 invoice is 61 days late, so three steps.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await AddPolicyAsync(harness, policy =>
        {
            policy.Kind = LateFeeKind.Percent;
            policy.Percent = 10m;
            policy.RepeatEveryDays = 30;
            policy.CapAmount = 25m;
        });
        var invoice = await IssueOverdueAsync(harness, daysLate: 61);

        // Act: one step per sweep.
        var made = new List<int>();

        for (var sweep = 0; sweep < 4; sweep++)
        {
            made.Add(await SweepAsync(harness));
        }

        // Assert
        Assert.Equal([1, 1, 1, 0], made);
        Assert.Equal([10m, 10m, 5m], (await DebitNotesAsync(harness, invoice.ItemId)).Select(note => note.Totals.Total));
    }

    [Fact]
    public async Task NoFee_WithinGrace_BelowTheMinimum_OnAnExemptInvoice_OrOnePaid()
    {
        // Arrange: $25 after five days of grace while at least $50 is due.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await AddPolicyAsync(harness, policy =>
        {
            policy.Kind = LateFeeKind.Flat;
            policy.Amount = 25m;
            policy.GraceDays = 5;
            policy.MinimumBalance = 50m;
        });

        var withinGrace = await IssueOverdueAsync(harness, daysLate: 3);
        var small = await IssueOverdueAsync(harness, daysLate: 10, draft => draft.Lines[0].UnitPrice = 20m);
        var exempt = await IssueOverdueAsync(harness, daysLate: 10, draft => draft.LateFeesExempt = true);
        var paid = await IssueOverdueAsync(harness, daysLate: 10);
        var payment = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(paid.TransactionId, 100m, "check", null, TestContext.Current.CancellationToken));

        // Act
        var made = await SweepAsync(harness);

        // Assert
        Assert.True(payment.Succeeded, string.Join(" ", payment.Errors));
        Assert.Equal(0, made);

        foreach (var invoice in new[] { withinGrace, small, exempt, paid })
        {
            Assert.Empty(await DebitNotesAsync(harness, invoice.ItemId));
        }
    }

    private static Task<int> SweepAsync(InvoicingTestHarness harness)
        => harness.InScopeAsync(services => services.GetRequiredService<ILateFeeService>().AssessDueAsync(_today, TestContext.Current.CancellationToken));

    private static Task<IReadOnlyList<Invoice>> DebitNotesAsync(InvoicingTestHarness harness, string invoiceId)
        => harness.InScopeAsync(services => services.GetRequiredService<ILateFeeService>().ListDebitNotesAsync(invoiceId, TestContext.Current.CancellationToken));

    private static async Task<Invoice> IssueOverdueAsync(InvoicingTestHarness harness, int daysLate, Action<Invoice> configure = null)
    {
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.IssueDate = _today.AddDays(-daysLate - 30);
            invoice.DueDate = _today.AddDays(-daysLate);
            configure?.Invoke(invoice);
        });

        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static Task<LateFeePolicy> AddPolicyAsync(InvoicingTestHarness harness, Action<LateFeePolicy> configure)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ILateFeePolicyManager>();
            var policy = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            policy.Name = "Late fee";
            configure(policy);

            var validation = await manager.ValidateAsync(policy, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(policy, TestContext.Current.CancellationToken);

            return policy;
        });
}
