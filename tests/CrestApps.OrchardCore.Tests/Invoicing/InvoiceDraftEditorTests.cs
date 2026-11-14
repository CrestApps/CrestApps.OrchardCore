using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The draft editor posts its rows as a plain form: rows keep their ids, a removed row goes, the blank row left to fill
/// in is ignored unless something was typed in it, and the bill-to is one value naming the party and its kind.
/// </summary>
public sealed class InvoiceDraftEditorTests
{
    [Fact]
    public void Apply_KeepsRowIds_DropsRemovedAndBlankRows_AndGivesANewRowAnId()
    {
        // Arrange
        var invoice = new Invoice { Currency = "USD" };
        invoice.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m });
        invoice.Lines.Add(new DocumentLine { Id = "line-2", Name = "Travel", Quantity = 1m, UnitPrice = 80m });

        var model = new InvoiceEditViewModel
        {
            Currency = "usd",
            Lines =
            [
                new InvoiceLineEditViewModel { Id = "line-1", Name = "Consulting", Quantity = 3m, UnitPrice = 50m, DiscountPercent = 10m },
                new InvoiceLineEditViewModel { Id = "line-2", Name = "Travel", Quantity = 1m, UnitPrice = 80m, Remove = true },
                new InvoiceLineEditViewModel { Kind = DocumentLineKind.Service, Name = "Setup", Quantity = 1m, UnitPrice = 25m, Unit = "job" },
                new InvoiceLineEditViewModel { Quantity = 1m },
            ],
        };

        // Act
        InvoiceDraftEditor.Apply(model, invoice);

        // Assert
        Assert.Equal("USD", invoice.Currency);
        Assert.Equal(2, invoice.Lines.Count);

        var kept = invoice.Lines[0];
        Assert.Equal(("line-1", 3m, 50m, 10m), (kept.Id, kept.Quantity, kept.UnitPrice, kept.DiscountPercent));

        var added = invoice.Lines[1];
        Assert.False(string.IsNullOrEmpty(added.Id));
        Assert.NotEqual("line-1", added.Id);
        Assert.Equal((DocumentLineKind.Service, "Setup", "job", 25m), (added.Kind, added.Name, added.Unit, added.UnitPrice));
    }

    [Fact]
    public void Apply_ReadsTheBillTo_AndTheContactTyped()
    {
        // Arrange
        var invoice = new Invoice();
        var model = new InvoiceEditViewModel
        {
            BillTo = new PartyRef(PartyKinds.Account, "account-1").ToString(),
            ContactName = " Fabrikam Ltd. ",
            ContactEmail = "ap@fabrikam.example",
            PurchaseOrderNumber = "PO-77",
            AllowPartialPayment = true,
        };

        // Act
        InvoiceDraftEditor.Apply(model, invoice);

        // Assert
        Assert.Equal(new PartyRef(PartyKinds.Account, "account-1"), invoice.BillTo.Party);
        Assert.Equal(("Fabrikam Ltd.", "ap@fabrikam.example"), (invoice.BillTo.Contact.Name, invoice.BillTo.Contact.Email));
        Assert.Equal("PO-77", invoice.PurchaseOrderNumber);
        Assert.True(invoice.AllowPartialPayment);
    }

    [Fact]
    public void Apply_WithNoBillTo_ClearsTheParty_AndKeepsTheContact()
    {
        // Arrange
        var invoice = new Invoice();
        invoice.BillTo.Party = new PartyRef(PartyKinds.User, "user-1");

        // Act
        InvoiceDraftEditor.Apply(new InvoiceEditViewModel { ContactName = "Walk-in customer", ContactEmail = "walkin@example.com" }, invoice);

        // Assert
        Assert.True(invoice.BillTo.Party.IsEmpty);
        Assert.Equal("Walk-in customer", invoice.BillTo.Contact.Name);
    }

    [Fact]
    public void Build_ShowsTheRows_AndOneBlankRowToFillIn()
    {
        // Arrange
        var invoice = new Invoice { Currency = "USD", BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.Customer, "customer-1"), Contact = new ContactInfo { Name = "Ann" } } };
        invoice.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m, Net = 100m, Total = 100m });

        // Act
        var model = new InvoiceEditViewModel();
        InvoiceDraftEditor.Build(invoice, model);

        // Assert
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal(("line-1", 100m), (model.Lines[0].Id, model.Lines[0].Total));
        Assert.Null(model.Lines[1].Id);
        Assert.Equal("Customer:customer-1", model.BillTo);
        Assert.Equal("Ann", model.ContactName);
    }

    [Fact]
    public void ACreditNoteDraft_ChangesOnlyAmounts_AndAddsNoLineOfItsOwn()
    {
        // Arrange
        var creditNote = new CreditNote { Currency = "USD" };
        creditNote.Lines.Add(new DocumentLine { Id = "credit-1", CreditedLineId = "line-1", Name = "Consulting", Description = "November", Unit = "hours", Quantity = 3m, UnitPrice = 50m });
        creditNote.Lines.Add(new DocumentLine { Id = "credit-2", CreditedLineId = "line-2", Name = "Travel", Quantity = 1m, UnitPrice = 80m });

        var model = new CreditNoteEditViewModel
        {
            Reason = " Two hours were not worked ",
            Disposition = CreditNoteDisposition.CustomerCredit,
            Lines =
            [
                new InvoiceLineEditViewModel { Id = "credit-1", Quantity = 2m, UnitPrice = 50m },
                new InvoiceLineEditViewModel { Id = "credit-2", Quantity = 1m, UnitPrice = 80m, Remove = true },
                new InvoiceLineEditViewModel { Name = "Something else", Quantity = 1m, UnitPrice = 999m },
            ],
        };

        // Act
        InvoiceDraftEditor.Apply(model, creditNote);

        // Assert
        Assert.Equal(("Two hours were not worked", CreditNoteDisposition.CustomerCredit), (creditNote.Reason, creditNote.Disposition));
        var line = Assert.Single(creditNote.Lines);
        Assert.Equal(("credit-1", "line-1", "Consulting", "November", "hours", 2m), (line.Id, line.CreditedLineId, line.Name, line.Description, line.Unit, line.Quantity));
    }

    [Fact]
    public async Task PartySearch_FindsEveryKindButGuests_AndSaysWhichKind()
    {
        // Arrange
        var directory = new Mock<IPartyDirectory>();
        directory.SetupGet(value => value.Kinds).Returns([PartyKinds.User, PartyKinds.Account, PartyKinds.Guest]);
        directory.Setup(value => value.SearchAsync(PartyKinds.User, "fab", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([new PartyOption("user-1", "Fabian")]);
        directory.Setup(value => value.SearchAsync(PartyKinds.Account, "fab", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([new PartyOption("account-1", "Fabrikam Ltd.")]);

        // Act
        var found = await InvoicePartySearchEndpoints.SearchAsync(directory.Object, "fab", 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["User:user-1", "Account:account-1"], found.Select(item => item.Value));
        Assert.Equal(["Fabian", "Fabrikam Ltd."], found.Select(item => item.Text));
        Assert.All(found, item => Assert.False(string.IsNullOrEmpty(item.Kind)));
        directory.Verify(value => value.SearchAsync(PartyKinds.Guest, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
