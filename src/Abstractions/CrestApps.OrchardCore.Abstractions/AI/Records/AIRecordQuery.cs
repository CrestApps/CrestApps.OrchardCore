namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// A page of records to list, with an optional text to filter them by.
/// </summary>
public sealed class AIRecordQuery
{
    /// <summary>
    /// Gets or sets the text the records' names must contain, or <see langword="null"/> for every record.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the page, starting at 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of records on a page.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
