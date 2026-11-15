using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A payment schedule set on an invoice (amounts or percents with due dates) is split on the ledger by the installment
/// plan service when the invoice is issued; the invoice then owes and is paid by what its payments owe and are paid, and
/// is voided by canceling them. A schedule that does not add up, or starts before the issue date, refuses the issue.
/// </summary>
public sealed class InvoicePaymentScheduleTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    [Fact]
    public async Task APercentSchedule_IsSplitOnTheLedgerAtIssue_AndTheInvoiceFollowsItsPayments()
    {
        // Arrange: a $1,000 invoice, 30% in two weeks and 70% in six.
        await using var harness = await InvoicingTestHarness.CreateAsync(configureServices: services => services.AddScoped<IInstallmentPlanService, FakeSchedulePlanService>());
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.Lines[0].UnitPrice = 500m;
            invoice.Schedule = [new() { DueDate = _today.AddDays(14), Percent = 30m }, new() { DueDate = _today.AddDays(42), Percent = 70m }];
        });

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var afterIssue = await harness.FindInvoiceAsync(draft.ItemId);
        var onReplacedDebt = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(afterIssue.TransactionId, 100m, "check", null, TestContext.Current.CancellationToken));
        var first = afterIssue.Schedule[0].TransactionId;
        await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(first, 300m, "check", null, TestContext.Current.CancellationToken));
        var afterFirst = await harness.FindInvoiceAsync(draft.ItemId);
        await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(afterIssue.Schedule[1].TransactionId, 700m, "check", null, TestContext.Current.CancellationToken));
        var afterSecond = await harness.FindInvoiceAsync(draft.ItemId);

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.False(string.IsNullOrEmpty(afterIssue.InstallmentPlanId));
        Assert.Equal([300m, 700m], afterIssue.Schedule.Select(part => part.IssuedAmount));
        Assert.Equal((InvoiceStatus.Open, 1000m, InvoiceSettlement.Unpaid), (afterIssue.Status, afterIssue.AmountDue, afterIssue.Settlement));
        Assert.Equal(TransactionStatus.Canceled, (await harness.FindTransactionAsync(afterIssue.TransactionId)).Status);
        Assert.False(onReplacedDebt.Succeeded);
        Assert.Equal((300m, 700m, InvoiceSettlement.PartiallyPaid), (afterFirst.AmountPaid, afterFirst.AmountDue, afterFirst.Settlement));
        Assert.Equal((1000m, 0m, InvoiceSettlement.Paid), (afterSecond.AmountPaid, afterSecond.AmountDue, afterSecond.Settlement));
    }

    [Fact]
    public async Task ASchedule_ThatDoesNotAddUp_OrStartsBeforeTheIssueDate_RefusesTheIssue()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(configureServices: services => services.AddScoped<IInstallmentPlanService, FakeSchedulePlanService>());
        var shortAmounts = await harness.CreateDraftAsync(invoice => invoice.Schedule = [new() { DueDate = _today.AddDays(10), Amount = 40m }, new() { DueDate = _today.AddDays(20), Amount = 40m }]);
        var early = await harness.CreateDraftAsync(invoice => invoice.Schedule = [new() { DueDate = _today.AddDays(-1), Percent = 50m }, new() { DueDate = _today.AddDays(20), Percent = 50m }]);
        var mixed = await harness.CreateDraftAsync(invoice => invoice.Schedule = [new() { DueDate = _today.AddDays(5), Percent = 50m }, new() { DueDate = _today.AddDays(20), Amount = 50m }]);

        // Act
        var results = new[]
        {
            await harness.RunAsync(commands => commands.IssueAsync(shortAmounts.ItemId, TestContext.Current.CancellationToken)),
            await harness.RunAsync(commands => commands.IssueAsync(early.ItemId, TestContext.Current.CancellationToken)),
            await harness.RunAsync(commands => commands.IssueAsync(mixed.ItemId, TestContext.Current.CancellationToken)),
        };

        // Assert
        Assert.All(results, result => Assert.False(result.Succeeded));
        Assert.Contains("adds up to 80.00", results[0].Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task VoidingAScheduledInvoice_CancelsItsPayments()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(configureServices: services => services.AddScoped<IInstallmentPlanService, FakeSchedulePlanService>());
        var draft = await harness.CreateDraftAsync(invoice => invoice.Schedule = [new() { DueDate = _today.AddDays(10), Amount = 60m }, new() { DueDate = _today.AddDays(20), Amount = 40m }]);
        await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Act: a write-off is refused (the payments carry the debt), then the invoice is voided.
        var writeOff = await harness.RunAsync(commands => commands.WriteOffAsync(draft.ItemId, "Never paid", TestContext.Current.CancellationToken));
        var voided = await harness.RunAsync(commands => commands.VoidAsync(draft.ItemId, "Billed twice", TestContext.Current.CancellationToken));

        // Assert
        Assert.False(writeOff.Succeeded);
        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));
        var invoice = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.All(invoice.Schedule, part => Assert.Equal(TransactionStatus.Canceled, harness.FindTransactionAsync(part.TransactionId).GetAwaiter().GetResult().Status));
    }

    /// <summary>
    /// Splits a balance into its scheduled payments on the ledger as the installment plan service does (its own tests
    /// cover the service itself), and cancels them.
    /// </summary>
    private sealed class FakeSchedulePlanService : IInstallmentPlanService
    {
        private static readonly Dictionary<string, List<string>> _plans = [];

        private readonly ITransactionManager _transactions;

        public FakeSchedulePlanService(ITransactionManager transactions)
        {
            _transactions = transactions;
        }

        public async Task<InstallmentPlanResult> CreateScheduleAsync(CreateInstallmentScheduleRequest request, CancellationToken cancellationToken = default)
        {
            var source = await _transactions.FindByIdAsync(request.TransactionId, cancellationToken);
            var plan = new InstallmentPlan { ItemId = Guid.NewGuid().ToString("N"), ReferenceType = source.ReferenceType, ReferenceId = source.ReferenceId, Status = InstallmentPlanStatus.Active };
            var ids = new List<string>();

            for (var i = 0; i < request.Payments.Count; i++)
            {
                var payment = await _transactions.NewAsync(cancellationToken: cancellationToken);
                payment.Title = $"Payment {i + 1}";
                payment.OwnerId = source.OwnerId;
                payment.OwnerKind = source.OwnerKind;
                payment.ReferenceType = "InstallmentPlan";
                payment.ReferenceId = plan.ItemId;
                payment.ParentReferenceType = source.ReferenceType;
                payment.ParentReferenceId = source.ReferenceId;
                payment.Currency = source.Currency;
                payment.Amount = request.Payments[i].Amount;
                payment.TotalAmount = request.Payments[i].Amount;
                payment.Status = TransactionStatus.Outstanding;
                payment.DueUtc = request.Payments[i].DueUtc;
                await _transactions.CreateAsync(payment, cancellationToken: cancellationToken);

                plan.Payments.Add(new InstallmentPlanPayment { Number = i + 1, Amount = request.Payments[i].Amount, DueUtc = request.Payments[i].DueUtc, TransactionId = payment.ItemId });
                ids.Add(payment.ItemId);
            }

            source.Status = TransactionStatus.Canceled;
            await _transactions.UpdateAsync(source, cancellationToken: cancellationToken);
            _plans[plan.ItemId] = ids;

            return InstallmentPlanResult.Success(plan);
        }

        public async Task<InstallmentPlanResult> CancelAsync(string planId, string reason, CancellationToken cancellationToken = default)
        {
            foreach (var id in _plans[planId])
            {
                var payment = await _transactions.FindByIdAsync(id, cancellationToken);
                payment.Status = TransactionStatus.Canceled;
                await _transactions.UpdateAsync(payment, cancellationToken: cancellationToken);
            }

            return InstallmentPlanResult.Success(new InstallmentPlan { ItemId = planId });
        }

        public Task<InstallmentPlanResult> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InstallmentPlanResult> CreateFromTransactionAsync(CreateInstallmentPlanFromTransactionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InstallmentPlanResult> ActivateAsync(string planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InstallmentPlanResult> ChargeAsync(string planId, int paymentNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InstallmentPlanResult> ProcessAsync(string planId, bool waitForLock = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
