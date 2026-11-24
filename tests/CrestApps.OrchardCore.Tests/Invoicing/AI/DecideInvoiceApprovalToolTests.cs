using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>decideInvoiceApproval</c> approves (which issues) or rejects an invoice waiting for approval as the caller, so
/// whoever submitted it can't approve it, and needs a reason to reject.
/// </summary>
public sealed class DecideInvoiceApprovalToolTests
{
    private static readonly InvoicingSettings _approvalOver50 = new() { ApprovalThreshold = 50m };

    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task DecideInvoiceApproval_RefusesACallerWithoutApproveInvoices_AndOneWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver50);
        var pending = await SubmitAsync(harness, "sam");
        var arguments = new Dictionary<string, object> { ["invoice"] = pending.ItemId, ["decision"] = "approve" };

        // Act
        var refused = await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), arguments, allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.ApproveInvoices));
        var anonymous = await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), arguments, signedIn: false);

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.PendingApproval, (await harness.FindInvoiceAsync(pending.ItemId)).Status);
    }

    [Fact]
    public async Task DecideInvoiceApproval_ApprovesAnotherUsersSubmission_WhichIssuesIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver50);
        var pending = await SubmitAsync(harness, "sam");

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), new() { ["invoice"] = pending.ItemId, ["decision"] = "approve" }));

        // Assert
        var issued = await harness.FindInvoiceAsync(pending.ItemId);
        Assert.Equal(InvoiceStatus.Open, issued.Status);
        Assert.Equal((issued.Number, "open"), (answer.GetProperty("number").GetString(), answer.GetProperty("status").GetString()));
        Assert.Equal(InvoicingLifecycleToolContext.CallerName, issued.Approval?.ApprovedBy);
    }

    [Fact]
    public async Task DecideInvoiceApproval_RefusesTheCallersOwnSubmission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver50);
        var pending = await SubmitAsync(harness, InvoicingLifecycleToolContext.CallerName);

        // Act
        var answer = await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), new() { ["invoice"] = pending.ItemId, ["decision"] = "approve" });

        // Assert
        Assert.StartsWith("Nothing was approved", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.PendingApproval, (await harness.FindInvoiceAsync(pending.ItemId)).Status);
    }

    [Fact]
    public async Task DecideInvoiceApproval_RejectsWithAReason_AndNeedsOne()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(_approvalOver50);
        var pending = await SubmitAsync(harness, "sam");

        // Act
        var noReason = await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), new() { ["invoice"] = pending.ItemId, ["decision"] = "reject" });
        var rejected = await _context.InvokeAsync(harness, new DecideInvoiceApprovalTool(), new() { ["invoice"] = pending.ItemId, ["decision"] = "reject", ["reason"] = "Wrong rate." });

        // Assert
        Assert.Contains("say why", noReason, StringComparison.Ordinal);
        Assert.Contains("draft again", rejected, StringComparison.Ordinal);
        var stored = await harness.FindInvoiceAsync(pending.ItemId);
        Assert.Equal((InvoiceStatus.Draft, "Wrong rate."), (stored.Status, stored.Approval?.RejectionReason));
    }

    [Fact]
    public void DecideInvoiceApproval_SaysItIsForInvoicesOnly()
    {
        // Act
        var description = new DecideInvoiceApprovalTool().Description;

        // Assert
        Assert.Contains("pendingApproval", description, StringComparison.Ordinal);
        Assert.Contains("decideApprovalRequest", description, StringComparison.Ordinal);
    }

    private static async Task<Invoice> SubmitAsync(InvoicingTestHarness harness, string submittedBy)
    {
        var draft = await harness.CreateDraftAsync();
        var submitted = await harness.RunAsync(commands => commands.SubmitForApprovalAsync(draft.ItemId, submittedBy, TestContext.Current.CancellationToken));

        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));

        return submitted.Invoice;
    }
}
