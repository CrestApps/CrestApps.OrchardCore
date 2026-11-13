namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A labelled value printed near the top of a business document, such as <c>Due date: May 31, 2026</c>.
/// </summary>
public sealed class BusinessDocumentField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentField"/> class.
    /// </summary>
    public BusinessDocumentField()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentField"/> class.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The value, already formatted.</param>
    public BusinessDocumentField(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the value, already formatted.
    /// </summary>
    public string Value { get; set; }
}
