using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An invoice at or above the approval amount waits for someone other than who submitted it to approve it before it is
/// issued, and goes back to a draft when rejected; a revision of an open invoice is a new draft that, once issued,
/// voids and replaces the original in the same step.
/// </summary>
public sealed class InvoiceApprovalTests
{
    private static readonly InvoicingSettings _approvalOver1000 = new() { ApprovalThreshold = 1000m };

    [Fact]
    public async Task AnInvoiceAtTheApprovalAmount_IsIssuedOnlyOnceSomeoneElseApprovesIt()
    {
        // Arrange: $1,500 and $500 drafts; approvals from $1,000.
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver1000);
        var large = await harness.CreateDraftAsync(invoice => invoice.Lines[0].UnitPrice = 750m);
        var small = await harness.CreateDraftAsync(invoice => invoice.Lines[0].UnitPrice = 250m);

        // Act
        var refused = await harness.RunAsync(commands => commands.IssueAsync(large.ItemId, TestContext.Current.CancellationToken));
        var submitted = await harness.RunAsync(commands => commands.SubmitForApprovalAsync(large.ItemId, "pat", TestContext.Current.CancellationToken));
        var issueWhileWaiting = await harness.RunAsync(commands => commands.IssueAsync(large.ItemId, TestContext.Current.CancellationToken));
        var selfApproved = await harness.RunAsync(commands => commands.ApproveAsync(large.ItemId, "pat", TestContext.Current.CancellationToken));
        var approved = await harness.RunAsync(commands => commands.ApproveAsync(large.ItemId, "sam", TestContext.Current.CancellationToken));
        var direct = await harness.RunAsync(commands => commands.IssueAsync(small.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(refused.Succeeded);
        Assert.True(refused.NeedsApproval);
        Assert.Equal(InvoiceStatus.Draft, refused.Invoice.Status);
        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));
        Assert.Equal((InvoiceStatus.PendingApproval, "pat"), (submitted.Invoice.Status, submitted.Invoice.Approval.SubmittedBy));
        Assert.False(issueWhileWaiting.Succeeded);
        Assert.False(selfApproved.Succeeded);
        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.Equal(InvoiceStatus.Open, approved.Invoice.Status);
        Assert.False(string.IsNullOrEmpty(approved.Invoice.Number));
        Assert.Equal("sam", approved.Invoice.Approval.ApprovedBy);
        Assert.Contains(approved.Invoice.History, entry => entry.Kind == DocumentEventKinds.Approved);
        Assert.True(direct.Succeeded, string.Join(" ", direct.Errors));
        Assert.Null(direct.Invoice.Approval);
    }

    [Fact]
    public async Task ARejectedInvoice_GoesBackToADraft_WithTheReason()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver1000);
        var draft = await harness.CreateDraftAsync(invoice => invoice.Lines[0].UnitPrice = 600m);
        await harness.RunAsync(commands => commands.SubmitForApprovalAsync(draft.ItemId, "pat", TestContext.Current.CancellationToken));

        // Act
        var noReason = await harness.RunAsync(commands => commands.RejectAsync(draft.ItemId, "sam", " ", TestContext.Current.CancellationToken));
        var rejected = await harness.RunAsync(commands => commands.RejectAsync(draft.ItemId, "sam", "Wrong rate", TestContext.Current.CancellationToken));
        var again = await harness.RunAsync(commands => commands.SubmitForApprovalAsync(draft.ItemId, "pat", TestContext.Current.CancellationToken));

        // Assert
        Assert.False(noReason.Succeeded);
        Assert.True(rejected.Succeeded, string.Join(" ", rejected.Errors));
        Assert.Equal(InvoiceStatus.Draft, rejected.Invoice.Status);
        Assert.Equal(("sam", "Wrong rate"), (rejected.Invoice.Approval.RejectedBy, rejected.Invoice.Approval.RejectionReason));
        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
        Assert.Equal(InvoiceStatus.PendingApproval, again.Invoice.Status);
        Assert.Null(again.Invoice.Approval.RejectedBy);
    }

    [Fact]
    public async Task IssuingARevision_VoidsAndReplacesTheOriginal_InOneStep()
    {
        // Arrange: INV for $100 issued, then revised to $120.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var original = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(original.ItemId, TestContext.Current.CancellationToken));

        // Act
        var revised = await harness.RunAsync(commands => commands.ReviseAsync(original.ItemId, TestContext.Current.CancellationToken));
        await harness.InScopeAsync(async services =>
        {
            var manager = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IInvoiceManager>(services);
            var draft = await manager.FindByIdAsync(revised.Invoice.ItemId, TestContext.Current.CancellationToken);
            draft.Lines[0].UnitPrice = 60m;
            await manager.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });
        var replacement = await harness.RunAsync(commands => commands.IssueAsync(revised.Invoice.ItemId, TestContext.Current.CancellationToken));
        var secondRevision = await harness.RunAsync(commands => commands.ReviseAsync(original.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded && revised.Succeeded && replacement.Succeeded, string.Join(" ", issued.Errors.Concat(revised.Errors).Concat(replacement.Errors)));
        Assert.Equal((InvoiceStatus.Draft, original.ItemId), (revised.Invoice.Status, revised.Invoice.RevisionOfId));
        Assert.Equal(["Consulting"], revised.Invoice.Lines.Select(line => line.Name));
        Assert.Equal((InvoiceStatus.Open, 120m), (replacement.Invoice.Status, replacement.Invoice.Totals.Total));

        var voided = await harness.FindInvoiceAsync(original.ItemId);
        Assert.Equal((InvoiceStatus.Void, replacement.Invoice.ItemId), (voided.Status, voided.ReplacedById));
        Assert.Contains(replacement.Invoice.Number, voided.VoidReason, StringComparison.Ordinal);
        Assert.Equal(TransactionStatus.Canceled, (await harness.FindTransactionAsync(issued.Invoice.TransactionId)).Status);
        Assert.False(secondRevision.Succeeded);
    }

    [Fact]
    public async Task AnInvoiceWithAPayment_IsNotRevised()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(issued.Invoice.TransactionId, 40m, "check", null, TestContext.Current.CancellationToken));

        // Act
        var revised = await harness.RunAsync(commands => commands.ReviseAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(revised.Succeeded);
    }
}
