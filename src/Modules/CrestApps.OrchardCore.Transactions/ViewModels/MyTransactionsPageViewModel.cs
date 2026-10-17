using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The customer's statement page in the site theme: its filter, and the statement shape it renders.
/// </summary>
public class MyTransactionsPageViewModel
{
    /// <summary>
    /// Gets or sets the title search, if any.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only the transactions with a balance still owed are listed.
    /// </summary>
    public bool Outstanding { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TransactionsConstants.Shapes.Statement"/> shape.
    /// </summary>
    public IShape Statement { get; set; }
}
