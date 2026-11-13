namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// The status badge of a business document, such as <c>Paid</c> or <c>Overdue</c>.
/// </summary>
public sealed class BusinessDocumentStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentStatus"/> class.
    /// </summary>
    public BusinessDocumentStatus()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentStatus"/> class.
    /// </summary>
    /// <param name="label">The text of the badge.</param>
    /// <param name="tone">The color of the badge.</param>
    public BusinessDocumentStatus(string label, BusinessDocumentTone tone)
    {
        Label = label;
        Tone = tone;
    }

    /// <summary>
    /// Gets or sets the text of the badge.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the color of the badge.
    /// </summary>
    public BusinessDocumentTone Tone { get; set; }
}
