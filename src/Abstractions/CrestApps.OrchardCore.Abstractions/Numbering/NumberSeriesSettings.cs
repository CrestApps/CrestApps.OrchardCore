namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// The formats a site chose for its number series. A series without an entry uses the format it was registered with.
/// </summary>
public sealed class NumberSeriesSettings
{
    /// <summary>
    /// Gets or sets the chosen formats, by series name.
    /// </summary>
    public Dictionary<string, NumberSeriesFormat> Formats { get; set; } = new(StringComparer.Ordinal);
}
