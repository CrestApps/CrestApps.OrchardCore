namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The page and filter of a customer statement built by <see cref="ITransactionCustomerPageService"/>.
/// </summary>
public sealed class TransactionStatementRequest
{
    /// <summary>
    /// The page size used when <see cref="PageSize"/> is not positive.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Gets or sets the one-based page number. A value below 1 loads the first page.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of transactions per page. A value of 0 or less uses <see cref="DefaultPageSize"/>.
    /// </summary>
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// Gets or sets a case-insensitive title search term.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only the transactions with a balance still owed are listed.
    /// </summary>
    public bool OutstandingOnly { get; set; }
}
