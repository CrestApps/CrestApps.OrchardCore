using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Reports.Widgets.Migrations;

/// <summary>
/// Defines the part that shows a report built in the report builder and the dashboard widget that shows one.
/// </summary>
internal sealed class ReportDesignWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ReportDesignWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(ReportWidgetConstants.DesignPartName, part => part
            .WithDisplayName("Custom Report Widget")
            .WithDescription("Shows a report or a view built in the report builder on the admin dashboard, for a period that moves with the clock, and keeps the result for a while."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(ReportWidgetConstants.DesignWidgetType, type => type
            .Stereotype(ReportWidgetConstants.DashboardWidgetStereotype)
            .WithDisplayName("Custom Report")
            .WithDescription("A report or a view built in the report builder, on the admin dashboard: pick it, its period and what to show.")
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(ReportWidgetConstants.DesignPartName, part => part.WithPosition("1")));

        return 1;
    }
}
