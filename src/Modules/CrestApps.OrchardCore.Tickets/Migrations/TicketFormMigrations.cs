using CrestApps.OrchardCore.Tickets.WebForm;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Adds the Ticket Form part and the Ticket Form widget type.
/// </summary>
internal sealed class TicketFormMigrations : DataMigration
{
    /// <summary>
    /// The widget content type.
    /// </summary>
    public const string WidgetType = "TicketFormWidget";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TicketFormMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(TicketFormPart), part => part
            .Attachable()
            .WithDisplayName("Ticket Form")
            .WithDescription("A form that opens a ticket of a chosen type on a chosen desk."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(WidgetType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(WidgetType, type => type
                .WithDisplayName("Ticket Form")
                .Stereotype("Widget")
                .WithPart(nameof(TicketFormPart)));
        }

        return 1;
    }
}
