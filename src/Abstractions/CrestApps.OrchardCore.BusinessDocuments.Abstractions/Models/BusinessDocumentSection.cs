namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A titled block of text printed after the totals of a business document, such as notes, terms or payment
/// instructions.
/// </summary>
public sealed class BusinessDocumentSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentSection"/> class.
    /// </summary>
    public BusinessDocumentSection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentSection"/> class.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="text">The text, whose line breaks are kept.</param>
    public BusinessDocumentSection(string title, string text)
    {
        Title = title;
        Text = text;
    }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the text, whose line breaks are kept.
    /// </summary>
    public string Text { get; set; }
}
