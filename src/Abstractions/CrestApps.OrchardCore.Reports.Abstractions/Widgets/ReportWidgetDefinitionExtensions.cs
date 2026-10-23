using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Defines ready-made report dashboard widgets, so a module adds one with a few lines in its migration.
/// </summary>
public static class ReportWidgetDefinitionExtensions
{
    /// <summary>
    /// Defines an admin dashboard widget type that shows a report.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="typeName">The technical name of the widget type.</param>
    /// <param name="displayName">The name the widget type is listed under when adding a widget.</param>
    /// <param name="description">What the widget shows.</param>
    /// <param name="settings">The report the widgets of the type show, and their defaults.</param>
    /// <returns>A task that completes when the type is defined.</returns>
    public static Task DefineReportWidgetAsync(
        this IContentDefinitionManager contentDefinitionManager,
        string typeName,
        string displayName,
        string description,
        ReportWidgetPartSettings settings)
    {
        ArgumentNullException.ThrowIfNull(contentDefinitionManager);
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentNullException.ThrowIfNull(settings);

        return contentDefinitionManager.AlterTypeDefinitionAsync(typeName, type => type
            .Stereotype(ReportWidgetConstants.DashboardWidgetStereotype)
            .WithDisplayName(displayName)
            .WithDescription(description)
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(ReportWidgetConstants.PartName, part => part
                .WithPosition("1")
                .WithSettings(settings)));
    }
}
