using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A credit note can never credit more than what is left to credit on each invoice line and on the invoice.
/// </summary>
public sealed class CreditNoteRulesTests
{
    [Fact]
    public void GetCreditable_SubtractsWhatIssuedCreditNotesCredited_AndIgnoresDraftsAndVoided()
    {
        // Arrange
        var invoice = CreateInvoice();
        var issued = Credit(CreditNoteStatus.Issued, ("l1", 30m));
        var draft = Credit(CreditNoteStatus.Draft, ("l1", 50m));
        var voided = Credit(CreditNoteStatus.Void, ("l2", 50m));

        // Act
        var creditable = CreditNoteRules.GetCreditable(invoice, [issued, draft, voided]);

        // Assert
        Assert.Equal(90m, creditable.Lines["l1"]);
        Assert.Equal(60m, creditable.Lines["l2"]);
        Assert.Equal(150m, creditable.Total);
    }

    [Fact]
    public void Validate_AcceptsACreditWithinWhatIsLeft()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var errors = CreditNoteRules.Validate(invoice, [Credit(CreditNoteStatus.Issued, ("l1", 100m))], Credit(CreditNoteStatus.Draft, ("l1", 20m), ("l2", 60m)));

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RefusesMoreThanIsLeftOnALine()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var errors = CreditNoteRules.Validate(invoice, [Credit(CreditNoteStatus.Issued, ("l1", 100m))], Credit(CreditNoteStatus.Draft, ("l1", 20.01m)));

        // Assert
        Assert.Contains(errors, error => error.LineId == "l1");
    }

    [Fact]
    public void Validate_RefusesALineTheInvoiceDoesNotHave_AndANoteThatCreditsNothing()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var unknown = CreditNoteRules.Validate(invoice, [], Credit(CreditNoteStatus.Draft, ("other", 5m)));
        var empty = CreditNoteRules.Validate(invoice, [], Credit(CreditNoteStatus.Draft));

        // Assert
        Assert.Contains(unknown, error => error.LineId == "other");
        Assert.NotEmpty(empty);
    }

    [Fact]
    public void Validate_RefusesMoreThanIsLeftOnTheInvoice_ForALineThatNamesNoInvoiceLine()
    {
        // Arrange: a free-standing credit line (a goodwill amount) still counts against the invoice total.
        var invoice = CreateInvoice();
        var note = Credit(CreditNoteStatus.Draft);
        note.Lines.Add(new DocumentLine { Id = "x", Total = 200m });
        note.Totals.Total = 200m;

        // Act
        var errors = CreditNoteRules.Validate(invoice, [], note);

        // Assert
        Assert.Contains(errors, error => error.LineId is null);
    }

    private static Invoice CreateInvoice()
    {
        var invoice = new Invoice { ItemId = "inv-1", Number = "INV-1001", Status = InvoiceStatus.Open, Currency = "USD" };
        invoice.Lines.Add(new DocumentLine { Id = "l1", Total = 120m });
        invoice.Lines.Add(new DocumentLine { Id = "l2", Total = 60m });
        invoice.Lines.Add(new DocumentLine { Id = "t", Kind = DocumentLineKind.Text });
        invoice.Totals.Total = 180m;

        return invoice;
    }

    private static CreditNote Credit(CreditNoteStatus status, params (string LineId, decimal Total)[] lines)
    {
        var note = new CreditNote { ItemId = Guid.NewGuid().ToString("n"), InvoiceId = "inv-1", Status = status };

        foreach (var (lineId, total) in lines)
        {
            note.Lines.Add(new DocumentLine { Id = Guid.NewGuid().ToString("n"), CreditedLineId = lineId, Total = total });
        }

        note.Totals.Total = lines.Sum(line => line.Total);

        return note;
    }
}
