using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The life cycle of an invoice and a credit note: which status follows which, and what an issued document still allows.
/// </summary>
public sealed class InvoiceLifecycleTests
{
    [Theory]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Open, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.PendingApproval, true)]
    [InlineData(InvoiceStatus.PendingApproval, InvoiceStatus.Open, true)]
    [InlineData(InvoiceStatus.PendingApproval, InvoiceStatus.Draft, true)]
    [InlineData(InvoiceStatus.Open, InvoiceStatus.Void, true)]
    [InlineData(InvoiceStatus.Open, InvoiceStatus.Uncollectible, true)]
    [InlineData(InvoiceStatus.Uncollectible, InvoiceStatus.Open, true)]
    [InlineData(InvoiceStatus.Open, InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Void, InvoiceStatus.Open, false)]
    [InlineData(InvoiceStatus.Void, InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Void, false)]
    [InlineData(InvoiceStatus.Uncollectible, InvoiceStatus.Void, false)]
    public void CanMove_FollowsTheLifeCycle(InvoiceStatus from, InvoiceStatus to, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, InvoiceLifecycle.CanMove(from, to));
    }

    [Fact]
    public void Void_IsTerminal()
    {
        // Act & Assert
        Assert.True(InvoiceLifecycle.IsTerminal(InvoiceStatus.Void));
        Assert.False(InvoiceLifecycle.IsTerminal(InvoiceStatus.Open));
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, true)]
    [InlineData(InvoiceStatus.PendingApproval, false)]
    [InlineData(InvoiceStatus.Open, false)]
    [InlineData(InvoiceStatus.Void, false)]
    public void CanEdit_OnlyADraft(InvoiceStatus status, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, InvoiceLifecycle.CanEdit(new Invoice { Status = status }));
    }

    [Fact]
    public void CanVoid_AnOpenInvoice_OnlyWhileNothingIsPaid()
    {
        // Arrange
        var unpaid = new Invoice { Status = InvoiceStatus.Open, AmountPaid = 0m };
        var paid = new Invoice { Status = InvoiceStatus.Open, AmountPaid = 10m };

        // Act & Assert
        Assert.True(InvoiceLifecycle.CanVoid(unpaid));
        Assert.False(InvoiceLifecycle.CanVoid(paid));
        Assert.False(InvoiceLifecycle.CanVoid(new Invoice { Status = InvoiceStatus.Draft }));
    }

    [Theory]
    [InlineData(InvoiceStatus.Open, InvoiceSettlement.Unpaid, "2026-03-10", "2026-03-11", true)]
    [InlineData(InvoiceStatus.Open, InvoiceSettlement.PartiallyPaid, "2026-03-10", "2026-03-11", true)]
    [InlineData(InvoiceStatus.Open, InvoiceSettlement.Unpaid, "2026-03-10", "2026-03-10", false)]
    [InlineData(InvoiceStatus.Open, InvoiceSettlement.Paid, "2026-03-10", "2026-03-11", false)]
    [InlineData(InvoiceStatus.Void, InvoiceSettlement.Unpaid, "2026-03-10", "2026-03-11", false)]
    [InlineData(InvoiceStatus.Draft, InvoiceSettlement.Unpaid, "2026-03-10", "2026-03-11", false)]
    public void IsOverdue_IsWorkedOutFromTheLocalDueDate(InvoiceStatus status, InvoiceSettlement settlement, string due, string today, bool expected)
    {
        // Arrange
        var invoice = new Invoice { Status = status, Settlement = settlement, DueDate = DateOnly.Parse(due) };

        // Act & Assert
        Assert.Equal(expected, InvoiceLifecycle.IsOverdue(invoice, DateOnly.Parse(today)));
    }

    [Theory]
    [InlineData(InvoiceDelivery.NotSent, InvoiceDelivery.Sent, InvoiceDelivery.Sent)]
    [InlineData(InvoiceDelivery.Sent, InvoiceDelivery.Viewed, InvoiceDelivery.Viewed)]
    [InlineData(InvoiceDelivery.Viewed, InvoiceDelivery.Sent, InvoiceDelivery.Viewed)]
    [InlineData(InvoiceDelivery.NotSent, InvoiceDelivery.Viewed, InvoiceDelivery.Viewed)]
    public void AdvanceDelivery_OnlyMovesForward(InvoiceDelivery current, InvoiceDelivery next, InvoiceDelivery expected)
    {
        // Act & Assert
        Assert.Equal(expected, InvoiceLifecycle.AdvanceDelivery(current, next));
    }

    [Theory]
    [InlineData(CreditNoteStatus.Draft, CreditNoteStatus.Issued, true)]
    [InlineData(CreditNoteStatus.Issued, CreditNoteStatus.Void, true)]
    [InlineData(CreditNoteStatus.Issued, CreditNoteStatus.Draft, false)]
    [InlineData(CreditNoteStatus.Void, CreditNoteStatus.Issued, false)]
    [InlineData(CreditNoteStatus.Draft, CreditNoteStatus.Void, false)]
    public void CreditNote_CanMove_FollowsItsLifeCycle(CreditNoteStatus from, CreditNoteStatus to, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, CreditNoteLifecycle.CanMove(from, to));
    }

    [Fact]
    public void CreditNote_CanVoid_OnlyWhileNothingWasRefunded()
    {
        // Act & Assert
        Assert.True(CreditNoteLifecycle.CanVoid(new CreditNote { Status = CreditNoteStatus.Issued }));
        Assert.False(CreditNoteLifecycle.CanVoid(new CreditNote { Status = CreditNoteStatus.Issued, AmountRefunded = 5m }));
        Assert.False(CreditNoteLifecycle.CanVoid(new CreditNote { Status = CreditNoteStatus.Draft }));
    }
}
