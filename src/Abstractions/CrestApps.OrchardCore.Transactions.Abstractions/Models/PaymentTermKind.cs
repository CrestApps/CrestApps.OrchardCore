namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// How a <see cref="PaymentTerm"/> turns the issue date of a document into its due date.
/// </summary>
public enum PaymentTermKind
{
    /// <summary>
    /// Due on the day the document is issued.
    /// </summary>
    DueOnReceipt = 0,

    /// <summary>
    /// Due a number of days after the issue date, such as Net 30.
    /// </summary>
    NetDays = 1,

    /// <summary>
    /// Due on the last day of the issue month, plus a number of days, such as end of month plus 15.
    /// </summary>
    EndOfMonth = 2,

    /// <summary>
    /// Due on a given day of the month after the issue month, such as the 10th of next month.
    /// </summary>
    DayOfNextMonth = 3,
}
