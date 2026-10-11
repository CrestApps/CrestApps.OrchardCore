using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// A reusable view: a saved query whose result columns other reports and views can read as a data set. A view joins,
/// filters, calculates, and aggregates once, so every report built on it shares the same prepared data.
/// </summary>
public sealed class ReportView : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<ReportView>
{
    /// <summary>
    /// Gets or sets the view name shown in the designer.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description shown in the designer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the query whose result the view exposes. Each result column becomes a field named after the
    /// column identifier.
    /// </summary>
    public ReportQueryDefinition Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the identifier of the user who owns the view.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who created the view.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the view was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the view was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets how often, in minutes, the view's result is refreshed and stored for the reports that read it.
    /// <c>0</c> runs the view live every time a report reads it. See <see cref="ReportViewRefreshIntervals"/>.
    /// </summary>
    public int RefreshIntervalMinutes { get; set; }

    /// <summary>
    /// Creates a deep copy of the view so a cached instance is never changed by the code that reads it.
    /// </summary>
    /// <returns>The copy.</returns>
    public ReportView Clone()
    {
        return JsonSerializer.Deserialize<ReportView>(JsonSerializer.SerializeToUtf8Bytes(this));
    }
}
