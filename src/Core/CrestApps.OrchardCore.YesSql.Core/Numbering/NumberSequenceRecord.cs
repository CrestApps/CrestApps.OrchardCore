namespace CrestApps.OrchardCore.YesSql.Core.Numbering;

/// <summary>
/// The last number issued in one series of the number sequence, stored as its own document so two series never write
/// the same document.
/// </summary>
public sealed class NumberSequenceRecord
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the series.
    /// </summary>
    public string Series { get; set; }

    /// <summary>
    /// Gets or sets the last number issued.
    /// </summary>
    public long Last { get; set; }
}
