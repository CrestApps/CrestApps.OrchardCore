using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Reports.Designer.Models;

namespace CrestApps.OrchardCore.Reports.Designer.ViewModels;

/// <summary>
/// The JSON the designer page sends and receives: a report or a view being designed.
/// </summary>
public class ReportDesignerPayload
{
    /// <summary>
    /// Gets or sets the identifier of the stored report or view, or <see langword="null"/> for a new one.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the report or the name of the view.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the report category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the query.
    /// </summary>
    public ReportQueryDefinition Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the report visuals.
    /// </summary>
    public IList<ReportVisualDefinition> Visuals { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the report is pinned to the admin menu.
    /// </summary>
    public bool ShowInAdminMenu { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether people the report is shared with may export it.
    /// </summary>
    public bool AllowExport { get; set; } = true;

    /// <summary>
    /// Gets or sets the user names the report is shared with.
    /// </summary>
    public IList<string> SharedUserNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles the report is shared with.
    /// </summary>
    public IList<string> SharedRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the values entered for exposed filters in the preview.
    /// </summary>
    public Dictionary<string, IList<string>> FilterValues { get; set; }

    /// <summary>
    /// Creates a payload from a stored report.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <returns>The payload.</returns>
    public static ReportDesignerPayload From(ReportDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        return new ReportDesignerPayload
        {
            Id = design.ItemId,
            DisplayText = design.DisplayText,
            Description = design.Description,
            Category = design.Category,
            Query = design.Query,
            Visuals = design.Visuals,
            ShowInAdminMenu = design.ShowInAdminMenu,
            AllowExport = design.AllowExport,
            SharedUserNames = design.SharedUserNames,
            SharedRoles = design.SharedRoles,
        };
    }

    /// <summary>
    /// Creates a payload from a stored view.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <returns>The payload.</returns>
    public static ReportDesignerPayload From(ReportView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new ReportDesignerPayload
        {
            Id = view.ItemId,
            DisplayText = view.DisplayText,
            Description = view.Description,
            Query = view.Query,
        };
    }

    /// <summary>
    /// Converts the payload to an unsaved report.
    /// </summary>
    /// <returns>The report.</returns>
    public ReportDesign ToDesign()
    {
        return new ReportDesign
        {
            ItemId = Id,
            DisplayText = DisplayText,
            Description = Description,
            Category = Category,
            Query = Query,
            Visuals = Visuals,
            ShowInAdminMenu = ShowInAdminMenu,
            AllowExport = AllowExport,
            SharedUserNames = SharedUserNames,
            SharedRoles = SharedRoles,
        };
    }

    /// <summary>
    /// Converts the payload to an unsaved view.
    /// </summary>
    /// <returns>The view.</returns>
    public ReportView ToView()
    {
        return new ReportView
        {
            ItemId = Id,
            DisplayText = DisplayText,
            Description = Description,
            Query = Query,
        };
    }
}

/// <summary>
/// The JSON options shared by the designer page and its endpoints: camel-case names and enums written as names.
/// </summary>
public static class ReportDesignerJson
{
    /// <summary>
    /// Gets the serializer options.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            MaxDepth = 32,
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
