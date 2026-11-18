namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// The number series the enabled features registered, by name.
/// </summary>
public sealed class NumberSeriesOptions
{
    /// <summary>
    /// Gets the registered series, by name.
    /// </summary>
    public Dictionary<string, NumberSeries> Series { get; } = new(StringComparer.Ordinal);
}
