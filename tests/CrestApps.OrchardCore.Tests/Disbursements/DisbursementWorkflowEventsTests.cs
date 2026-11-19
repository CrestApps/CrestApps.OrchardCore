using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Workflows;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The Disbursements workflow events: each change of a disbursement and of a batch starts its event, the service tells
/// the batch observers each step of a batch and a returns file, and the inputs a workflow reads.
/// </summary>
public sealed class DisbursementWorkflowEventsTests
{
    [Theory]
    [InlineData(DisbursementChangeKind.Created, new[] { nameof(DisbursementCreatedEvent) })]
    [InlineData(DisbursementChangeKind.Batched, new[] { nameof(DisbursementBatchedEvent) })]
    [InlineData(DisbursementChangeKind.Unbatched, new[] { nameof(DisbursementUnbatchedEvent) })]
    [InlineData(DisbursementChangeKind.Submitted, new[] { nameof(DisbursementSubmittedEvent) })]
    [InlineData(DisbursementChangeKind.Failed, new[] { nameof(DisbursementFailedEvent) })]
    [InlineData(DisbursementChangeKind.Canceled, new[] { nameof(DisbursementCanceledEvent) })]
    [InlineData(DisbursementChangeKind.Reversed, new[] { nameof(DisbursementReversedEvent) })]
    public void EachDisbursementChange_StartsItsEvents(DisbursementChangeKind kind, string[] expected)
        => Assert.Equal(expected, DisbursementsWorkflowObserver.EventsFor(kind));

    [Fact]
    public void EveryDisbursementChange_StartsAtMostOneEvent_AndOnlyAFailureStartsDisbursementFailed()
    {
        foreach (var kind in Enum.GetValues<DisbursementChangeKind>())
        {
            var events = DisbursementsWorkflowObserver.EventsFor(kind);

            Assert.True(events.Length <= 1, $"{kind} starts {string.Join(", ", events)}; a change starts one event.");
            Assert.Equal(kind == DisbursementChangeKind.Failed, events.Contains(nameof(DisbursementFailedEvent)));
        }
    }

    [Fact]
    public void EachBatchChange_StartsItsEvents_AndAReleasePaidInFullSettlesToo()
    {
        static string[] For(DisbursementBatchChangeKind kind, DisbursementBatchStatus status = DisbursementBatchStatus.Released)
            => DisbursementsWorkflowObserver.EventsFor(new DisbursementBatchChange { Kind = kind, Batch = new DisbursementBatch { Status = status } });

        Assert.Equal([nameof(DisbursementBatchCreatedEvent)], For(DisbursementBatchChangeKind.Created));
        Assert.Equal([nameof(DisbursementBatchReleasedEvent)], For(DisbursementBatchChangeKind.Released));
        Assert.Equal([nameof(DisbursementBatchReleasedEvent), nameof(DisbursementBatchSettledEvent)], For(DisbursementBatchChangeKind.Released, DisbursementBatchStatus.Settled));
        Assert.Equal([nameof(DisbursementBatchReopenedEvent)], For(DisbursementBatchChangeKind.Reopened));
        Assert.Equal([nameof(DisbursementBatchCanceledEvent)], For(DisbursementBatchChangeKind.Canceled));
        Assert.Equal([nameof(DisbursementBatchSettledEvent)], For(DisbursementBatchChangeKind.Settled));
    }

    [Fact]
    public async Task TheService_TellsTheBatchObservers_EachStepOfABatch()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var first = await context.AchAsync(origin, payee, 10m, "statement-a");
        var second = await context.AchAsync(origin, payee, 20m, "statement-b");

        // Act: one batch cancelled, another released, settled and returned.
        var (canceled, _) = await context.Service.CreateBatchAsync("Nacha", [first.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.CancelBatchAsync(canceled, TestContext.Current.CancellationToken);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [first.ItemId, second.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        await context.Service.ApplyReturnsAsync([new DisbursementReturn(second.TraceNumber, "R03", "No account"), new DisbursementReturn("999", "R01", "x")], TestContext.Current.CancellationToken);
        var settled = await context.Service.SettleBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        var changes = context.BatchObserver.Changes;

        Assert.Equal(
            [DisbursementBatchChangeKind.Created, DisbursementBatchChangeKind.Canceled, DisbursementBatchChangeKind.Created, DisbursementBatchChangeKind.Released, DisbursementBatchChangeKind.Settled],
            changes.Select(change => change.Kind));
        Assert.Equal((DisbursementBatchStatus.Open, DisbursementBatchStatus.Canceled), (changes[1].PreviousStatus, changes[1].Batch.Status));
        Assert.Equal((batch.ItemId, DisbursementBatchStatus.Open), (changes[3].Batch.ItemId, changes[3].PreviousStatus));
        Assert.Equal(DisbursementBatchStatus.Released, changes[4].PreviousStatus);
        Assert.Equal((1, settled), (changes[4].Settled, 1));

        var returns = Assert.Single(context.BatchObserver.Returns);
        Assert.Equal((1, 1), (returns.Returned, returns.Problems.Count));

        // The disbursements tell their own steps: added, removed, added again.
        Assert.Contains(context.Observer.Changes, change => change.Kind == DisbursementChangeKind.Unbatched && change.Disbursement.ItemId == first.ItemId);
    }

    [Fact]
    public void ABatchsInput_NamesTheBatchAndNeverItsFile()
    {
        var batch = new DisbursementBatch { ItemId = "b1", Provider = "Nacha", SourceBankAccountId = "a1", SourceBankAccountName = "Payroll funding", EffectiveDate = new DateOnly(2026, 11, 16), Count = 2, TotalAmount = 30m, Currency = "USD", Status = DisbursementBatchStatus.Released, FileName = "b1.ach" };

        var input = DisbursementsWorkflowObserver.BatchInput(new DisbursementBatchChange { Kind = DisbursementBatchChangeKind.Released, Batch = batch, PreviousStatus = DisbursementBatchStatus.Open, Actor = "pat" });

        Assert.Equal(("b1", "Nacha", "a1", "2026-11-16", 2), (input["BatchId"], input["Provider"], input["SourceBankAccountId"], input["EffectiveDate"], input["Count"]));
        Assert.Equal((30m, "USD", "Released", "Open", "Released"), (input["TotalAmount"], input["Currency"], input["Status"], input["PreviousStatus"], input["Change"]));
        Assert.Equal(("b1.ach", "pat"), (input["FileName"], input["Actor"]));

        var returns = DisbursementsWorkflowObserver.ReturnsInput(new DisbursementImportResult { Returned = 2, Problems = ["No match"] }, "pat");

        Assert.Equal((2, 1, "pat"), (returns["Returned"], returns["Problems"], returns["Actor"]));
        Assert.Equal(["No match"], (string[])returns["ProblemMessages"]);
    }
}
