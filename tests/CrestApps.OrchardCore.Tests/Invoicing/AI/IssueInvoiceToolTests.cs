using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>issueInvoice</c> issues a draft as the Issue button does, e-mails it only to the customer's own addresses, submits
/// a draft that needs approval when asked, and refuses what would move money or void another invoice without a preview:
/// a draft that charges a saved card and a revision.
/// </summary>
public sealed class IssueInvoiceToolTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task IssueInvoice_RefusesACallerWithoutIssueInvoices_AndOneWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();
        var arguments = new Dictionary<string, object> { ["invoice"] = draft.ItemId };

        // Act
        var refused = await _context.InvokeAsync(harness, new IssueInvoiceTool(), arguments, allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.IssueInvoices));
        var anonymous = await _context.InvokeAsync(harness, new IssueInvoiceTool(), arguments, signedIn: false);
        var sendWithoutManage = await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId, ["send"] = true }, allow: InvoicingLifecycleToolContext.Only(InvoicingPermissions.IssueInvoices));

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Contains("not allowed", sendWithoutManage, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(draft.ItemId)).Status);
    }

    [Fact]
    public async Task IssueInvoice_IssuesTheDraft_AndSendsItToTheCustomerOnly()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId, ["send"] = true }));

        // Assert
        var issued = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal(InvoiceStatus.Open, issued.Status);
        Assert.False(string.IsNullOrEmpty(issued.TransactionId));
        Assert.Equal((issued.Number, "open", true), (answer.GetProperty("number").GetString(), answer.GetProperty("status").GetString(), answer.GetProperty("sent").GetBoolean()));
        var email = Assert.Single(harness.Emails);
        Assert.Equal("ap@fabrikam.example", email.Email.To);
    }

    [Fact]
    public async Task IssueInvoice_TakesNoRecipient()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        var answer = await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId, ["send"] = true, ["to"] = "attacker@example.com" });

        // Assert
        Assert.Contains("'to' is not an argument", answer, StringComparison.Ordinal);
        Assert.Empty(harness.Emails);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(draft.ItemId)).Status);
    }

    [Fact]
    public async Task IssueInvoice_RefusesADraftThatChargesASavedCard()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.AutoCharge = true;
            invoice.BillTo = new DocumentParty
            {
                Party = new PartyRef(PartyKinds.User, "user-9"),
                Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" },
            };
        });

        // Act
        var answer = await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId });

        // Assert
        Assert.Contains("charges the customer's saved card", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(draft.ItemId)).Status);
        Assert.Empty(harness.ChargedCards);
    }

    [Fact]
    public async Task IssueInvoice_RefusesARevision_AndTheInvoiceItRevisesStaysOpen()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var original = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var revised = await harness.RunAsync(commands => commands.ReviseAsync(original.ItemId, TestContext.Current.CancellationToken));
        Assert.True(revised.Succeeded, string.Join(" ", revised.Errors));

        // Act
        var answer = await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = revised.Invoice.ItemId });

        // Assert
        Assert.Contains($"revises invoice {original.Number}", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(original.ItemId)).Status);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(revised.Invoice.ItemId)).Status);
    }

    [Fact]
    public async Task IssueInvoice_SaysWhenTheDraftNeedsApproval_AndSubmitsItWhenAsked()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { ApprovalThreshold = 50m });
        var draft = await harness.CreateDraftAsync();

        // Act
        var needs = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId }));
        var submitted = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new IssueInvoiceTool(), new() { ["invoice"] = draft.ItemId, ["submitForApproval"] = true }, allow: InvoicingLifecycleToolContext.Only(InvoicingPermissions.ManageInvoices)));

        // Assert
        Assert.True(needs.GetProperty("needsApproval").GetBoolean());
        Assert.True(submitted.GetProperty("submittedForApproval").GetBoolean());
        var stored = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal((InvoiceStatus.PendingApproval, InvoicingLifecycleToolContext.CallerName), (stored.Status, stored.Approval?.SubmittedBy));
    }

    [Fact]
    public async Task IssueInvoice_ThatNeedsApproval_CanBeSubmittedInTheSameMessage_AndKeepsTheMessagesEarlierWork()
    {
        // Arrange: the approval is decided before the issue command runs, so nothing of the message is discarded. The draft
        // carries its worked-out totals, as a draft saved from its page or by draftInvoice does.
        await using var harness = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { ApprovalThreshold = 50m });
        var needsApproval = await harness.CreateDraftAsync(invoice => invoice.Totals = new DocumentTotals { Subtotal = 100m, Total = 100m });
        Invoice earlier = null;

        // Act
        var answers = await _context.InvokeInOneMessageAsync(
            harness,
            async services => earlier = await CreateUnsavedDraftAsync(services),
            (new IssueInvoiceTool(), new() { ["invoice"] = needsApproval.ItemId }),
            (new IssueInvoiceTool(), new() { ["invoice"] = needsApproval.ItemId, ["submitForApproval"] = true }));

        // Assert
        Assert.Contains("needsApproval", answers[0], StringComparison.Ordinal);
        Assert.Contains("submittedForApproval", answers[1], StringComparison.Ordinal);
        Assert.NotNull(await harness.FindInvoiceAsync(earlier.ItemId));
        Assert.Equal(InvoiceStatus.PendingApproval, (await harness.FindInvoiceAsync(needsApproval.ItemId)).Status);
    }

    [Fact]
    public async Task IssueInvoice_WritesEarlierWorkOfTheMessageFirst_AndRefusesMoreChangesAfterARefusedCommand()
    {
        // Arrange: the issue command refuses a draft billed to nobody and discards the session's unsaved work.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var billedToNobody = await harness.CreateDraftAsync(invoice => invoice.BillTo = new DocumentParty());
        var other = await harness.CreateDraftAsync();
        Invoice earlier = null;

        // Act
        var answers = await _context.InvokeInOneMessageAsync(
            harness,
            async services => earlier = await CreateUnsavedDraftAsync(services),
            (new IssueInvoiceTool(), new() { ["invoice"] = billedToNobody.ItemId }),
            (new IssueInvoiceTool(), new() { ["invoice"] = other.ItemId }));

        // Assert
        Assert.Contains("Nothing was issued", answers[0], StringComparison.Ordinal);
        Assert.Contains("earlier change in this message was refused", answers[1], StringComparison.Ordinal);
        Assert.NotNull(await harness.FindInvoiceAsync(earlier.ItemId));
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(other.ItemId)).Status);
    }

    // A draft created earlier in the same chat message, not saved yet.
    private static async Task<Invoice> CreateUnsavedDraftAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IInvoiceManager>();
        var invoice = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

        invoice.Currency = "USD";
        invoice.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Northwind", Email = "ap@northwind.example" } };
        invoice.Lines.Add(new DocumentLine { Id = "line-1", Name = "Support", Quantity = 1m, UnitPrice = 20m });
        await manager.CreateAsync(invoice, TestContext.Current.CancellationToken);

        return invoice;
    }
}
