namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A column of the line table of a business document.
/// </summary>
public sealed class BusinessDocumentColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentColumn"/> class.
    /// </summary>
    public BusinessDocumentColumn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentColumn"/> class.
    /// </summary>
    /// <param name="name">The name the cells of a row are keyed by.</param>
    /// <param name="title">The header of the column.</param>
    /// <param name="alignment">How the values of the column are aligned.</param>
    /// <param name="width">The relative width of the column, or 0 to size it to its content.</param>
    public BusinessDocumentColumn(string name, string title, BusinessDocumentAlignment alignment = BusinessDocumentAlignment.Start, double width = 0)
    {
        Name = name;
        Title = title;
        Alignment = alignment;
        Width = width;
    }

    /// <summary>
    /// Gets or sets the name the cells of a row are keyed by, such as <c>Quantity</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the header of the column.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how the values of the column are aligned.
    /// </summary>
    public BusinessDocumentAlignment Alignment { get; set; }

    /// <summary>
    /// Gets or sets the width of the column relative to the other columns that have one, such as 3 for a column three
    /// times as wide as one of 1. A column of 0 is sized to its content.
    /// </summary>
    public double Width { get; set; }
}
