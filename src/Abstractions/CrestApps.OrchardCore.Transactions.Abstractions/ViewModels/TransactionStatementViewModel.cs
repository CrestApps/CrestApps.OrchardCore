namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// One page of a customer's statement of their own transactions. It is the model of the <c>TransactionStatement</c>
/// shape (<see cref="TransactionsConstants.Shapes.Statement"/>).
/// </summary>
public class TransactionStatementViewModel
{
    /// <summary>
    /// Gets or sets the transactions on the current page, most recent first.
    /// </summary>
    public IReadOnlyList<TransactionStatementEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets how many transactions match the statement's filter across every page.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the title search the statement was filtered by, if any.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the statement lists only the transactions with a balance still owed.
    /// </summary>
    public bool OutstandingOnly { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered under the statement, or <see langword="null"/> for no pager. Whoever
    /// renders the statement sets it, because only they know the address the pages link to.
    /// </summary>
    public object Pager { get; set; }

    /// <summary>
    /// Copies every value of this model to <paramref name="target"/>, for example to initialize the
    /// <see cref="TransactionsConstants.Shapes.Statement"/> shape from a model built by
    /// <see cref="Services.ITransactionCustomerPageService"/>.
    /// </summary>
    /// <param name="target">The model to copy to.</param>
    public void CopyTo(TransactionStatementViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Entries = Entries;
        target.TotalCount = TotalCount;
        target.Search = Search;
        target.OutstandingOnly = OutstandingOnly;
        target.Pager = Pager;
    }
}
