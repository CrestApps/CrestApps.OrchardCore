using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// A report designed in the report designer: the query that reads and shapes its data, the visuals that present the
/// result, where it appears in the admin menu, and who it is shared with.
/// </summary>
public sealed class ReportDesign : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<ReportDesign>
{
    /// <summary>
    /// Gets or sets the report title.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description shown above the report.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category the report is listed under, in the admin menu and the report list.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the query that reads and shapes the report data.
    /// </summary>
    public ReportQueryDefinition Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the visuals, in display order.
    /// </summary>
    public IList<ReportVisualDefinition> Visuals { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the report has its own item under Reports in the admin menu.
    /// </summary>
    public bool ShowInAdminMenu { get; set; }

    /// <summary>
    /// Gets or sets the user names of the people the report is shared with.
    /// </summary>
    public IList<string> SharedUserNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the roles the report is shared with. The built-in <c>Anonymous</c> role shares the
    /// report with everyone, including visitors who are not signed in; <c>Authenticated</c> shares it with everyone
    /// who is signed in.
    /// </summary>
    public IList<string> SharedRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether people the report is shared with may export it.
    /// </summary>
    public bool AllowExport { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifier of the user who owns the report. The report reads its data with the owner's access.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who created the report.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the report was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the report was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Creates a deep copy of the report so a cached instance is never changed by the code that reads it.
    /// </summary>
    /// <returns>The copy.</returns>
    public ReportDesign Clone()
    {
        return JsonSerializer.Deserialize<ReportDesign>(JsonSerializer.SerializeToUtf8Bytes(this));
    }
}
