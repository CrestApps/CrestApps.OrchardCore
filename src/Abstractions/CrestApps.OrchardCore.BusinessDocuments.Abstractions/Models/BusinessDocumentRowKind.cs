namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// What a row of a business document's line table is.
/// </summary>
public enum BusinessDocumentRowKind
{
    /// <summary>
    /// A line item, with a value in each column.
    /// </summary>
    Item,

    /// <summary>
    /// A heading that groups the rows under it, printed bold across the row.
    /// </summary>
    Section,

    /// <summary>
    /// A subtotal of the rows above it, printed bold with a rule above.
    /// </summary>
    Subtotal,

    /// <summary>
    /// A note printed in smaller, lighter text across the row.
    /// </summary>
    Note,
}
