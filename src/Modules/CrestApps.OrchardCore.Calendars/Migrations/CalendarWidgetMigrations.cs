using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Calendars.Migrations;

/// <summary>
/// Defines the part that shows calendars and the dashboard widget that shows them.
/// </summary>
internal sealed class CalendarWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CalendarWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(CalendarWidgetPart.PartName, part => part
            .WithDisplayName("Calendar Widget")
            .WithDescription("Shows calendars on the admin dashboard: the viewer's own, every calendar of a type, or chosen calendars."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(CalendarWidgetPart.WidgetType, type => type
            .Stereotype("DashboardWidget")
            .WithDisplayName("Calendar")
            .WithDescription("Calendars on the admin dashboard: pick the viewer's own calendars, a calendar type or chosen calendars, and the view to open on.")
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(CalendarWidgetPart.PartName, part => part.WithPosition("1")));

        return 1;
    }
}
