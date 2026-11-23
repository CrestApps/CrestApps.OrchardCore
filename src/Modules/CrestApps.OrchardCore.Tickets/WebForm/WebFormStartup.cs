using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Tickets.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Registers the Ticket Form widget. Its trap field and its challenge (ReCaptcha, when that feature is on) are the
/// shared public form protection of the CrestApps Resources feature.
/// </summary>
[Feature(TicketsConstants.Features.WebForm)]
public sealed class WebFormStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TicketFormPart>()
            .UseDisplayDriver<TicketFormPartDisplayDriver>();
        services.AddDataMigration<TicketFormMigrations>();
    }
}
