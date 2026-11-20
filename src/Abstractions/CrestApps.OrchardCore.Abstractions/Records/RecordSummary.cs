namespace CrestApps.OrchardCore.Records;

/// <summary>
/// What a list or a picker shows of a record it only refers to.
/// </summary>
public sealed class RecordSummary
{
    /// <summary>
    /// Gets or sets the record's id in its kind.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the record's title, such as an order number or an asset's name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a short second line, such as a status or a date. Optional.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the address of the record's admin page, or <see langword="null"/> when it has none.
    /// </summary>
    public string AdminUrl { get; set; }
}
