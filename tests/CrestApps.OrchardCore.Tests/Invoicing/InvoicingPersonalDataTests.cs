using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A person's invoices are exported and, on erasure, lose their contact details while keeping what the business must
/// keep; invoices billed to a business account are not theirs.
/// </summary>
public sealed class InvoicingPersonalDataTests
{
    [Fact]
    public async Task Export_ListsTheInvoicesBilledToThePerson_AndTheirCreditNotes_NotTheirAccounts()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (mine, _) = await SeedAsync(harness);
        var export = new CommercePersonalDataExport();

        // Act
        await harness.InScopeAsync(async services =>
        {
            var subject = new CommercePersonalDataSubject(CustomerOwner.ForUser("user-1"), null);
            var contributor = Contributor(services);

            await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);
            await contributor.ExportAsync(new CommercePersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);

            Assert.Contains("pat@example.com", subject.Emails);

            return true;
        });

        // Assert
        var invoice = Assert.Single(export.Records, record => record.Kind == InvoicingPersonalDataContributor.InvoiceRecordKind);
        Assert.Equal((mine.ItemId, mine.Number, "Pat Doe", "pat@example.com", "100"), (invoice.Id, invoice.Values["Number"], invoice.Values["BillToName"], invoice.Values["BillToEmail"], invoice.Values["Total"]));
        Assert.Single(export.Records, record => record.Kind == InvoicingPersonalDataContributor.CreditNoteRecordKind);
    }

    [Fact]
    public async Task Anonymize_ScrubsTheContact_KeepsNumbersAndAmounts_AndSaysSo()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (mine, theirs) = await SeedAsync(harness);
        var result = new CommercePersonalDataAnonymizeResult();

        // Act
        await harness.InScopeAsync(async services =>
        {
            var subject = new CommercePersonalDataSubject(CustomerOwner.ForUser("user-1"), "pat@example.com");
            subject.AddName("Pat Doe");

            await Contributor(services).AnonymizeAsync(new CommercePersonalDataAnonymizeContext(subject, "[removed]", result), TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var stored = await harness.FindInvoiceAsync(mine.ItemId);
        Assert.Equal(("[removed]", null, null), (stored.BillTo.Contact.Name, stored.BillTo.Contact.Email, stored.BillTo.Contact.Address));
        Assert.Empty(stored.BillTo.CopyTo);
        Assert.Equal((mine.Number, 100m, "TAX-9"), (stored.Number, stored.Totals.Total, stored.BillTo.TaxId));
        Assert.DoesNotContain(stored.History, entry => (entry.Message ?? string.Empty).Contains("Pat Doe", StringComparison.Ordinal));
        Assert.DoesNotContain("Pat Doe", stored.Notes ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal((1, 1), (result.Records[InvoicingPersonalDataContributor.InvoiceRecordKind], result.Records[InvoicingPersonalDataContributor.CreditNoteRecordKind]));
        Assert.Single(result.Warnings);

        var account = await harness.FindInvoiceAsync(theirs.ItemId);
        Assert.Equal("Contoso Ltd.", account.BillTo.Contact.Name);
    }

    [Fact]
    public async Task Export_FindsAnInvoiceBilledByEmailAlone_ButNotABusinessAccountsUnderTheSameEmail()
    {
        // Arrange: a guest's invoice with only a contact, and an account's invoice that uses the same address.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var guest = await IssueAsync(harness, invoice => invoice.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Sam Roe", Email = "Sam@Example.com" } });
        await IssueAsync(harness, invoice => invoice.BillTo = new DocumentParty
        {
            Party = new PartyRef(PartyKinds.Account, "acct-2"),
            Contact = new ContactInfo { Name = "Roe Trading", Email = "sam@example.com" },
        });
        var export = new CommercePersonalDataExport();

        // Act
        await harness.InScopeAsync(async services =>
        {
            var subject = new CommercePersonalDataSubject(null, "SAM@example.com");

            await Contributor(services).ExportAsync(new CommercePersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal(guest.ItemId, record.Id);
    }

    private static InvoicingPersonalDataContributor Contributor(IServiceProvider services)
        => new(
            services.GetRequiredService<IInvoiceStore>(),
            services.GetRequiredService<ICreditNoteStore>(),
            services.GetRequiredService<IPartyMembershipService>(),
            new Telephony.Doubles.PassThroughStringLocalizer<InvoicingPersonalDataContributor>());

    // An invoice billed to the person, credited, and one billed to an account they may work for.
    private static async Task<(Invoice Mine, Invoice Theirs)> SeedAsync(InvoicingTestHarness harness)
    {
        var mine = await IssueAsync(harness, invoice =>
        {
            invoice.BillTo = new DocumentParty
            {
                Party = new PartyRef(PartyKinds.User, "user-1"),
                Contact = new ContactInfo { Name = "Pat Doe", Email = "pat@example.com", Address = new Address { AddressLine1 = "1 Main St" } },
                TaxId = "TAX-9",
                CopyTo = ["accounts@example.com"],
            };
            invoice.Notes = "Thanks, Pat Doe!";
        });

        var draft = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(mine.ItemId, TestContext.Current.CancellationToken));
        await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.CreditNote.ItemId, TestContext.Current.CancellationToken));

        var theirs = await IssueAsync(harness, invoice => invoice.BillTo = new DocumentParty
        {
            Party = new PartyRef(PartyKinds.Account, "acct-1"),
            Contact = new ContactInfo { Name = "Contoso Ltd.", Email = "ap@contoso.example" },
        });

        return (mine, theirs);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, Action<Invoice> configure)
    {
        var draft = await harness.CreateDraftAsync(configure);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
