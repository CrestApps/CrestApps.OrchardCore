namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// When the numbers of a series start again from the first number.
/// </summary>
public enum NumberSeriesReset
{
    /// <summary>
    /// The numbers never start again.
    /// </summary>
    Never,

    /// <summary>
    /// The numbers start again each calendar year.
    /// </summary>
    Yearly,

    /// <summary>
    /// The numbers start again each calendar month.
    /// </summary>
    Monthly,
}
