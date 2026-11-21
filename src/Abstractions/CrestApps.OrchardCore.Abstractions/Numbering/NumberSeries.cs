using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// A registered series of numbers, such as invoice numbers: its name, the title a settings page shows for it, and how
/// its numbers are written unless the site chose another format.
/// </summary>
public sealed class NumberSeries
{
    /// <summary>
    /// Gets or sets the name of the series, such as <c>Financial.Invoice</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown for the series.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource Title { get; set; }

    /// <summary>
    /// Gets or sets how the numbers are written when the site has not chosen a format.
    /// </summary>
    public NumberSeriesFormat DefaultFormat { get; set; } = new();
}
