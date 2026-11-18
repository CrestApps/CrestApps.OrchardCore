using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Tickets.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Registers the Ticket Form widget.
/// </summary>
[Feature(TicketsConstants.Features.WebForm)]
public sealed class WebFormStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TicketFormPart>()
            .UseDisplayDriver<TicketFormPartDisplayDriver>();
        services.AddSingleton<TicketFormGuard>();
        services.AddDataMigration<TicketFormMigrations>();
    }
}

/// <summary>
/// Adds the ticket web form's challenge when Orchard Core's ReCaptcha is on.
/// </summary>
[Feature(TicketsConstants.Features.WebForm)]
[RequireFeatures(OrchardCoreFeatureIds.ReCaptcha)]
public sealed class WebFormReCaptchaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ITicketFormChallenge, ReCaptchaTicketFormChallenge>();
}
