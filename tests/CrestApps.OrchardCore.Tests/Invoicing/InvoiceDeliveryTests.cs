using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An invoice is delivered as a business document: printed, kept as the PDF it was issued with, sent by e-mail with that
/// PDF, and shown to the customer by a signed link that records the first time they open it.
/// </summary>
public sealed class InvoiceDeliveryTests
{
    [Fact]
    public async Task TheDocument_ShowsLinesTaxesTotalsAndWhatIsDue_AndADraftIsMarked()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.Lines.Add(new DocumentLine { Id = "line-2", Kind = DocumentLineKind.Section, Name = "Parts" });
            invoice.Lines.Add(new DocumentLine { Id = "line-3", Name = "Filter", Quantity = 2m, UnitPrice = 10m, DiscountPercent = 50m, Unit = "pcs" });
            invoice.Notes = "Thank you for your business.";
        });

        // Act
        var draftDocument = await BuildAsync(harness, await harness.FindInvoiceAsync(draft.ItemId));
        var issued = (await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken))).Invoice;
        var issuedDocument = await BuildAsync(harness, await harness.FindInvoiceAsync(draft.ItemId));

        // Assert
        Assert.Equal(("Draft", InvoicingConstants.BusinessDocumentKinds.Invoice), (draftDocument.Watermark, draftDocument.Kind));
        Assert.DoesNotContain(draftDocument.Totals, total => total.Label == "Amount due");

        Assert.Null(issuedDocument.Watermark);
        Assert.Equal(issued.Number, issuedDocument.Number);
        Assert.Equal(3, issuedDocument.Rows.Count);
        Assert.Equal("2 pcs", issuedDocument.Rows[2].Cells["quantity"]);
        Assert.Contains("Less 50%", issuedDocument.Rows[2].Detail, StringComparison.Ordinal);
        Assert.Contains(issuedDocument.Fields, field => field.Label == "Due");
        Assert.Contains(issuedDocument.Sections, section => section.Text == "Thank you for your business.");

        var due = Assert.Single(issuedDocument.Totals, total => total.Label == "Amount due");
        Assert.True(due.Emphasized);
        Assert.Contains("110.00", due.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIssuedInvoice_KeepsThePdfItWasIssuedWith()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act: issued, then the look changes.
        await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        harness.Pdf.Version = "v2";
        var stored = await harness.FindInvoiceAsync(draft.ItemId);
        var pdf = await harness.InScopeAsync(services => services.GetRequiredService<IInvoicePdfService>().GetAsync(stored, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(string.IsNullOrEmpty(stored.ArchiveFile));
        Assert.Equal($"Invoice|{stored.Number}||v1", System.Text.Encoding.UTF8.GetString(pdf.Content));
        Assert.Equal($"{stored.Number}.pdf", pdf.FileName);
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(draft.ItemId));
    }

    [Fact]
    public async Task Sending_EmailsThePdfToTheBillTo_AndRecordsIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var sent = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest
        {
            InvoiceId = invoice.ItemId,
            Cc = ["owner@fabrikam.example", " "],
        }, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));

        var (email, content) = Assert.Single(harness.Emails);
        Assert.Equal((InvoicingConstants.EmailKinds.InvoiceIssued, "ap@fabrikam.example", invoice.Number), (email.Kind, email.To, email.Reference));
        Assert.Equal(["owner@fabrikam.example"], email.Cc);
        Assert.Equal(invoice.ItemId, ((Invoice)email.Models[InvoicingConstants.EmailKinds.DocumentModelName]).ItemId);

        var attachment = Assert.Single(content.Attachments);
        Assert.Equal(($"{invoice.Number}.pdf", "application/pdf"), (attachment.FileName, attachment.ContentType));
        Assert.Equal($"https://contoso.example/invoices/view/{invoice.ItemId}", content.Values[InvoicingConstants.EmailKinds.ViewUrlValueName]);

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal(InvoiceDelivery.Sent, stored.Delivery);
        Assert.NotNull(stored.SentUtc);
        Assert.Contains(stored.History, entry => entry.Kind == DocumentEventKinds.Sent && entry.Message.Contains("owner@fabrikam.example", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADraft_IsSentOnlyWhenSendingIssuesIt()
    {
        // Arrange
        await using var refusing = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { IssueOnSend = false });
        await using var issuing = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { IssueOnSend = true });
        var kept = await refusing.CreateDraftAsync();
        var sentDraft = await issuing.CreateDraftAsync();

        // Act
        var refused = await refusing.InScopeAsync(services => services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = kept.ItemId }, TestContext.Current.CancellationToken));
        var issued = await issuing.InScopeAsync(services => services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = sentDraft.ItemId }, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(refused.Succeeded);
        Assert.Empty(refusing.Emails);
        Assert.Equal(InvoiceStatus.Draft, (await refusing.FindInvoiceAsync(kept.ItemId)).Status);

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.True(issued.Issued);
        Assert.Single(issuing.Emails);
        Assert.Equal((InvoiceStatus.Open, InvoiceDelivery.Sent), ((await issuing.FindInvoiceAsync(sentDraft.ItemId)).Status, (await issuing.FindInvoiceAsync(sentDraft.ItemId)).Delivery));
    }

    [Fact]
    public async Task WithoutEmail_NothingIsSent()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        harness.CanSendEmail = false;

        // Act
        var sent = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = invoice.ItemId }, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(sent.Succeeded);
        Assert.Empty(harness.Emails);
        Assert.Equal(InvoiceDelivery.NotSent, (await harness.FindInvoiceAsync(invoice.ItemId)).Delivery);
    }

    [Fact]
    public async Task AShareLink_NamesItsInvoice_AndATamperedOneIsRefused()
    {
        // Arrange
        var clock = Mock.Of<IClock>(value => value.UtcNow == InvoicingTestHarness.Now);
        var site = new SiteSettings();
        site.Put(new InvoicingSettings { ShareLinkDays = 30 });
        var links = new InvoiceShareLinkService(
            new SignedTokenService(new EphemeralDataProtectionProvider(), clock),
            urls: null,
            Mock.Of<ISiteService>(service => service.GetSiteSettingsAsync() == Task.FromResult<ISite>(site)));

        // Act
        var token = await links.CreateTokenAsync(new Invoice { ItemId = "invoice-1" });

        // Assert
        Assert.Equal("invoice-1", links.GetInvoiceId(token));
        Assert.Null(links.GetInvoiceId(token[..^2] + "xx"));
        Assert.Null(links.GetInvoiceId(null));
    }

    [Fact]
    public async Task TheFirstOpenByTheCustomer_IsRecordedOnce()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var first = await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceViewTracker>().RecordAsync(await services.GetRequiredService<IInvoiceStore>().FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
        var again = await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceViewTracker>().RecordAsync(await services.GetRequiredService<IInvoiceStore>().FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(first);
        Assert.False(again);

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal(InvoiceDelivery.Viewed, stored.Delivery);
        Assert.Single(stored.History, entry => entry.Kind == DocumentEventKinds.Viewed);
    }

    private static Task<CrestApps.OrchardCore.BusinessDocuments.Models.BusinessDocument> BuildAsync(InvoicingTestHarness harness, InvoicingDocument document)
        => harness.InScopeAsync(services => services.GetRequiredService<IInvoiceDocumentBuilder>().BuildAsync(document, TestContext.Current.CancellationToken));

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
