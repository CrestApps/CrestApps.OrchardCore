namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A line of the totals of a business document, such as the subtotal, a tax or the amount due.
/// </summary>
public sealed class BusinessDocumentTotal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentTotal"/> class.
    /// </summary>
    public BusinessDocumentTotal()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentTotal"/> class.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The amount, already formatted.</param>
    /// <param name="emphasized">Whether the line is the one the reader looks for, such as the total.</param>
    /// <param name="muted">Whether the line is printed lighter, such as a tax line.</param>
    public BusinessDocumentTotal(string label, string value, bool emphasized = false, bool muted = false)
    {
        Label = label;
        Value = value;
        Emphasized = emphasized;
        Muted = muted;
    }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the amount, already formatted.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the line is the one the reader looks for, such as the total or the amount
    /// due: it is printed bold and larger.
    /// </summary>
    public bool Emphasized { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the line is printed lighter, such as a tax line.
    /// </summary>
    public bool Muted { get; set; }
}
