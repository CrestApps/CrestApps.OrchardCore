using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Dashboard;

/// <summary>
/// The store's sales at a glance, on the admin dashboard: today, the last 7 and 30 days, and the orders to ship.
/// </summary>
public sealed class SalesSummaryPart : ContentPart
{
}

/// <summary>
/// Adds the Sales Summary dashboard widget.
/// </summary>
public sealed class SalesSummaryWidgetMigrations : DataMigration
{
    public const string WidgetType = "SalesSummaryWidget";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SalesSummaryWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(SalesSummaryPart), part => part
            .WithDisplayName("Sales Summary")
            .WithDescription("Shows the store's sales today and over the last 7 and 30 days, and the orders to ship."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(WidgetType, type => type
            .Stereotype("DashboardWidget")
            .WithDisplayName("Sales Summary")
            .WithDescription("The store's sales and orders to ship, on the admin dashboard.")
            .Draftable(false)
            .Versionable(false)
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(nameof(SalesSummaryPart), part => part.WithPosition("1")));

        return 1;
    }
}
