using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A customer's statement in one currency: what they owe now (an open-item statement, with its aging), or what was
/// charged and paid in a period with the balance before and after it (an activity statement).
/// </summary>
public sealed class InvoiceStatement
{
    /// <summary>
    /// Gets or sets what was asked for.
    /// </summary>
    public InvoiceStatementRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the currency of every amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the customer, as on their latest invoice.
    /// </summary>
    public DocumentParty Recipient { get; set; }

    /// <summary>
    /// Gets or sets the business, as on the customer's latest invoice.
    /// </summary>
    public DocumentIssuer Issuer { get; set; }

    /// <summary>
    /// Gets or sets the balance before the period (activity statements).
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Gets or sets the balance at the end of the period, or what is owed now on an open-item statement. A negative
    /// balance is credit the customer holds.
    /// </summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>
    /// Gets the entries of the period, oldest first, with the running balance (activity statements).
    /// </summary>
    public IList<InvoiceStatementLine> Lines { get; } = [];

    /// <summary>
    /// Gets the invoices still owed, oldest due first (open-item statements).
    /// </summary>
    public IList<InvoiceStatementOpenItem> OpenItems { get; } = [];

    /// <summary>
    /// Gets what is owed by how late it is: not due yet, 1 to 30, 31 to 60, 61 to 90 and over 90 days.
    /// </summary>
    public decimal[] Aging { get; } = new decimal[5];

    /// <summary>
    /// Gets the other currencies the customer has invoices in, each on a statement of its own.
    /// </summary>
    public IList<string> OtherCurrencies { get; } = [];
}

/// <summary>
/// What a statement is for.
/// </summary>
public sealed class InvoiceStatementRequest
{
    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets the kind of statement.
    /// </summary>
    public InvoiceStatementKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period of an activity statement, in the site's time zone.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period, or the day of an open-item statement, in the site's time zone.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// Gets or sets the currency, or <see langword="null"/> for the currency of the customer's latest invoice.
    /// </summary>
    public string Currency { get; set; }
}

/// <summary>
/// The kinds of statement.
/// </summary>
public enum InvoiceStatementKind
{
    /// <summary>
    /// The invoices still owed, with how late each is.
    /// </summary>
    OpenItem = 0,

    /// <summary>
    /// What was charged and paid in a period, with the balance before and after it.
    /// </summary>
    Activity = 1,
}

/// <summary>
/// One entry of an activity statement.
/// </summary>
public sealed class InvoiceStatementLine
{
    public DateOnly Date { get; set; }

    public InvoiceStatementLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the number of the invoice, debit note or credit note the entry belongs to.
    /// </summary>
    public string DocumentNumber { get; set; }

    public string InvoiceId { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Gets or sets what the entry adds to the balance.
    /// </summary>
    public decimal Charge { get; set; }

    /// <summary>
    /// Gets or sets what the entry takes off the balance.
    /// </summary>
    public decimal Credit { get; set; }

    /// <summary>
    /// Gets or sets the balance after the entry.
    /// </summary>
    public decimal Balance { get; set; }
}

/// <summary>
/// The kinds of statement entries.
/// </summary>
public enum InvoiceStatementLineKind
{
    Invoice = 0,
    Payment = 1,
    CreditNote = 2,
    Adjustment = 3,
    Reversal = 4,
    Refund = 5,
}

/// <summary>
/// An invoice still owed on an open-item statement.
/// </summary>
public sealed class InvoiceStatementOpenItem
{
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets how many days after its due date it is, or zero when it is not due yet.
    /// </summary>
    public int DaysOverdue { get; set; }
}
