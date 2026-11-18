using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The administration detail page of a transaction: the transaction, for the page title, and its shape, built by the
/// display drivers of <see cref="Transaction"/>.
/// </summary>
public class TransactionDetailPageViewModel
{
    /// <summary>
    /// Gets or sets the transaction.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the shape of the transaction for the detail display type.
    /// </summary>
    public IShape Shape { get; set; }
}
