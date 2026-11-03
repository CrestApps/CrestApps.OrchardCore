using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// A group of designed reports. Groups can be nested; a report placed in a group is listed under it in the admin
/// Reports menu and in the report list.
/// </summary>
public sealed class ReportGroup : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<ReportGroup>
{
    /// <summary>
    /// Gets or sets the name of the group.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the group this group is nested in, or <see langword="null"/> for a group at the
    /// top level.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the display order among the groups that share its parent. Lower numbers come first; groups with the
    /// same order are sorted by name.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the group was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the group was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Creates a deep copy of the group so a cached instance is never changed by the code that reads it.
    /// </summary>
    /// <returns>The copy.</returns>
    public ReportGroup Clone()
    {
        return JsonSerializer.Deserialize<ReportGroup>(JsonSerializer.SerializeToUtf8Bytes(this));
    }
}
