namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The status selection offered on the administration transactions report. It maps to a
/// <see cref="Models.TransactionStatus"/> filter, with the extra <see cref="All"/> and
/// <see cref="Outstanding"/> options that a report needs to answer "what has not been paid yet".
/// </summary>
public enum TransactionStatusFilter
{
    /// <summary>
    /// No status filter is applied.
    /// </summary>
    All = 0,

    /// <summary>
    /// Only transactions with a balance still owed are returned.
    /// </summary>
    Outstanding = 1,

    /// <summary>
    /// Only pending transactions are returned.
    /// </summary>
    Pending = 2,

    /// <summary>
    /// Only partially paid transactions are returned.
    /// </summary>
    PartiallyPaid = 3,

    /// <summary>
    /// Only fully paid transactions are returned.
    /// </summary>
    Paid = 4,

    /// <summary>
    /// Only canceled transactions are returned.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// Only failed transactions are returned.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// Only abandoned transactions are returned.
    /// </summary>
    Abandoned = 7,

    /// <summary>
    /// Only refunded transactions are returned.
    /// </summary>
    Refunded = 8,
}
