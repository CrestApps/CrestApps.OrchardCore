namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// The editor of a transaction workflow event.
/// </summary>
public class TransactionEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the sources whose transactions start the workflow, comma separated.
    /// </summary>
    public string Sources { get; set; }

    /// <summary>
    /// Gets or sets the reference types whose transactions start the workflow, comma separated.
    /// </summary>
    public string ReferenceTypes { get; set; }
}
